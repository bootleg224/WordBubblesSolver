using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using AForge;
using AForge.Imaging;
using AForge.Imaging.Filters;
using AForge.Math.Geometry;
using WordBubblesSolver.Web.App;
using Point = AForge.Point;

namespace WordBubblesSolver.Web.Controllers
{
    public class PuzzleModel
    {
        public string Data { get; set; }
        public int Smallest { get; set; }
        public int Largest { get; set; }
        public List<string[]> Solutions { get; set; }
        public string Base64Image { get; set; }
    }

    //Code sample modified from www.dotnetperls.com/word-search
    internal enum WordType : byte
    {
        FullWord,
        PartialWord,
        FullWordAndPartialWord
    }

    public class Letter
    {
        public string L{ get; set; }
        public float X { get; set; }
        public float Y{ get; set; }
        public float Radius { get; set; }
    }

    public class HomeController : Controller
    {


        public ActionResult Index()
        {            
            return View(new PuzzleModel
            {
                Data = "",
                Largest = 10,
                Smallest = 3,
                Solutions = new List<string[]>()
            });
        }


        [HttpPost]
        public ActionResult Twilio(string Body)
        {
            var solutions = SolvePuzzle(Body, 4, 10);
            var sb = new StringBuilder();

            foreach (var solution in solutions)
            {
                foreach (var s in solution)
                    sb.AppendLine(s);

                sb.AppendLine();
            }

            
            return
                Content(
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<Response><Message>" +
                    sb.ToString().Substring(0, Math.Min(sb.Length, 1600)) + "</Message></Response>", "text/xml");
        }

        [HttpPost]
        public ActionResult Index(string Data, int Smallest = 3, int Largest = 6, HttpPostedFileBase file = null)
        {
            string base64Image = null;
            if (file != null)
            {
                //try to determine data from posted file
                var bitmap = new Bitmap(file.InputStream);

                // lock image
                BitmapData bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                    ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);



                // step 1 - turn background to black
                var filter = new Invert();
                filter.ApplyInPlace(bitmapData);

                ColorFiltering colorFilter = new ColorFiltering();

                colorFilter.Red = new IntRange(0, 64);
                colorFilter.Green = new IntRange(0, 64);
                colorFilter.Blue = new IntRange(0, 64);
                colorFilter.FillOutsideRange = false;

                colorFilter.ApplyInPlace(bitmapData);


                // step 2 - locating objects
                BlobCounter blobCounter = new BlobCounter();

                blobCounter.FilterBlobs = true;
                blobCounter.MinHeight = 5;
                blobCounter.MinWidth = 5;

                blobCounter.ProcessImage(bitmapData);
                var blobs = blobCounter.GetObjectsInformation();
                bitmap.UnlockBits(bitmapData);
                base64Image = bitmap.ToBase64();
                // get information about detected objects            
                var shapeChecker = new SimpleShapeChecker();

                var letters = new List<Letter>();

                int circleCount = 0;
                foreach (
                    var blob in
                        blobs.ToArray()
                            .OrderBy(b => b.Rectangle.Top)
                            .ThenBy(b => b.Rectangle.Left)
                            .ThenByDescending(b => b.Area))
                {

                    List<IntPoint> edgePoints = blobCounter.GetBlobsEdgePoints(blob);

                    AForge.Point center;
                    float radius;

                    if (shapeChecker.IsCircle(edgePoints, out center, out radius))
                    {

                        //Todo: filter on the largest radius * 90% to deal with resolutions
                        if (radius < 40)
                            continue;


                        blobCounter.ExtractBlobsImage(bitmap, blob, false);
                        var letter = blob.Image.ToManagedImage(true);
                        var resizeFilter = new ResizeBilinear(150, 150);
                        letter = resizeFilter.Apply(letter);                        
                        
                        var bwBitmap = new Bitmap(75, 75, PixelFormat.Format32bppArgb);


                        for (int y = 40; y < 115; y++)
                        {
                            for (int x = 40; x < 115; x++)
                            {
                                var color = letter.GetPixel(x, y);
                                if (color.Name == "ff000000")
                                {
                                    bwBitmap.SetPixel(x - 40, y - 40, Color.Black);
                                }
                                else
                                {
                                    bwBitmap.SetPixel(x - 40, y - 40, Color.White);
                                }
                            }                        
                        }

                       letters.Add(new Letter
                       {
                           L = TrainingData.GetBestGuess(bwBitmap),
                           X = center.X,
                           Y = center.Y,
                           Radius = radius
                       }); 
                    }
                }

                var minX = letters.Min(c => c.X);
                var maxX = letters.Max(c => c.X);

                var minY = letters.Min(c => c.Y);
                var maxY = letters.Max(c => c.Y);

                var smallestRadius = letters.Min(c => c.Radius);

                var numberOfItemsPerRow = (int)((maxX - minX)/ smallestRadius / 2);
                var numberOfItemsPerCol = (int)((maxY - minY) / smallestRadius / 2);

                var spaceBetweenPointsX = (maxX - minX)/numberOfItemsPerRow;
                var spaceBetweenPointsY = (maxY - minY) / numberOfItemsPerCol;


                var varianceDelta = smallestRadius*.05f; //allow 5% pixel float


                var puzzle = new StringBuilder();
                for (var y = minY; y <= maxY; y += spaceBetweenPointsY)
                {

                    for (var x = minX; x <= maxX; x += spaceBetweenPointsX)
                    {
                        var item = letters.FirstOrDefault(c => c.X > x - varianceDelta && c.X < x + varianceDelta
                                                               && c.Y > y - varianceDelta && c.Y < y + varianceDelta);

                        if (item != null)
                            puzzle.Append(item.L);
                        else
                            puzzle.Append("_");
                    }

                    puzzle.AppendLine();
                }

                Data = puzzle.ToString();

            }

            var solutions = SolvePuzzle(Data, Smallest, Largest);
            return View(new PuzzleModel
            {
                Data = Data,
                Largest = Largest,
                Smallest = Smallest,
                Solutions = solutions,
                Base64Image = base64Image
            });

        }

        public List<string[]> SolvePuzzle(string data, int smallest, int largest)
        {
            var solutions = new List<string[]>();

            data = data.ToLower();
            var letters = "abcdefghijklmnopqrstuvwxyz".ToCharArray();
            var letterCount = data.ToCharArray().Count(letters.Contains);

            //load dictionary of words
            using (var reader = new StreamReader(Server.MapPath("~/App_Data/Dictionary/enable1.txt")))
            {
                while (true)
                {
                    var line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }

                    //ignore smallest words
                    if (line.Length < smallest) continue;

                    for (var i = 1; i <= line.Length; i++)
                    {
                        var substring = line.Substring(0, i);
                        WordType value;
                        if (_words.TryGetValue(substring, out value))
                        {
                            // If this is a full word.
                            if (i == line.Length)
                            {
                                // If only partial word is stored.
                                if (value == WordType.PartialWord)
                                {
                                    // Upgrade type.
                                    _words[substring] = WordType.FullWordAndPartialWord;
                                }
                            }
                            else
                            {
                                // If not a full word and only partial word is stored.
                                if (value == WordType.FullWord)
                                {
                                    _words[substring] = WordType.FullWordAndPartialWord;
                                }
                            }
                        }
                        else
                        {
                            // If this is a full word.
                            if (i == line.Length)
                            {
                                _words.Add(substring, WordType.FullWord);
                            }
                            else
                            {
                                _words.Add(substring, WordType.PartialWord);
                            }
                        }
                    }
                }
            }

            // Get puzzle grid.
            var pattern = data;


            // Split on newlines.
            var lines = pattern.Split(new char[] {'\r', '\n'},
                StringSplitOptions.RemoveEmptyEntries);

            // Put into 2D array.
            var height = lines.Length;
            var width = lines[0].Length;
            var array = new char[height, width];
            for (var i = 0; i < width; i++)
            {
                for (int a = 0; a < height; a++)
                {
                    array[a, i] = lines[a][i];
                }
            }
            // Create empty covered array. (true if we already traversed this point)
            var covered = new bool[height, width];

            // Start at each square in the 2D array.
            for (var i = 0; i < width; i++)
            {
                for (var a = 0; a < height; a++)
                {
                    Search(array, i, a, width, height, "", covered);
                }
            }


            //Get solutions of 10 words or less
            //var allPossibleSolutions = GetAllPossibleCombinations().Where(c => c.Sum(x => x.Length) <= letterCount);

            //foreach(var potentialSolution in allPossibleSolutions)
            //{                
            //    ////remove solutions that use the same letter more than once
            //    //var allPoints = potentialSolution.SelectMany(c => c.Value);
            //    //var enumerable = allPoints as string[] ?? allPoints.ToArray();

            //    //var solutionUsedSameLetterTwice = false;
            //    //foreach (var p in enumerable)
            //    //{
            //    //    if (enumerable.Count(c => c == p) > 1)
            //    //    {
            //    //        //solutionUsedSameLetterTwice = true;
            //    //        //break;
            //    //    }
            //    //}

            //    //if(!solutionUsedSameLetterTwice)
            //    //    solutions.Add(potentialSolution.Select(c=> c.Key).ToArray());
            //}

            solutions.Add(FoundWords);
            return solutions;
        }


        private IEnumerable<int> constructSetFromBits(int i)
        {
            for (int n = 0; i != 0; i /= 2, n++)
            {
                if ((i & 1) != 0)
                    yield return n;
            }
        }

        
        private IEnumerable<List<string>> produceEnumeration()
        {
            for (int i = 0; i < (1 << _found.Count); i++)
            {
                yield return
                    constructSetFromBits(i).Select(n => FoundWords[n]).ToList();
            }
        }

        public List<string>[] GetAllPossibleCombinations()
        {
            return produceEnumeration().ToArray();
        }

        private void Search(char[,] array,
            int i,
            int a,
            int width,
            int height,
            string build,
            bool[,] covered)
        {
            // Don't go past around array bounds.
            if (i >= width ||
                i < 0 ||
                a >= height ||
                a < 0)
            {
                return;
            }
            // Don't deal with already covered squares.
            if (covered[a, i])
            {
                return;
            }
            // Get letter.
            char letter = array[a, i];

            //ignore blanks
            if (letter == ' ' || letter == '_')
                return;


            // Append.
            var pass = build + letter;
            // See if full word.
            WordType value;
            if (_words.TryGetValue(pass, out value))
            {
                // Handle all full words.
                if (value == WordType.FullWord ||
                    value == WordType.FullWordAndPartialWord)
                {
                    var currentPointsUsed = new List<string>(); //keep track of point locations
                    for (int i2 = 0; i2 < width; i2++)
                    {
                        for (int a2 = 0; a2 < height; a2++)
                        {
                            if (covered[a2, i2])
                                currentPointsUsed.Add(string.Format("{0}_{1}", i2, a2));
                        }
                    }

                    _found.Add(new KeyValuePair<string, List<string>>(pass, currentPointsUsed));

                }
                // Handle all partial words.
                if (value != WordType.PartialWord && value != WordType.FullWordAndPartialWord) return;

                // Copy covered array.
                var cov = new bool[height, width];
                for (int i2 = 0; i2 < width; i2++)
                {
                    for (int a2 = 0; a2 < height; a2++)
                    {
                        cov[a2, i2] = covered[a2, i2];
                    }
                }
                // Set this current square as covered.
                cov[a, i] = true;

                // Continue in all directions. [8]
                Search(array, i + 1, a, width, height, pass, cov);
                Search(array, i, a + 1, width, height, pass, cov);
                Search(array, i + 1, a + 1, width, height, pass, cov);
                Search(array, i - 1, a, width, height, pass, cov);
                Search(array, i, a - 1, width, height, pass, cov);
                Search(array, i - 1, a - 1, width, height, pass, cov);
                Search(array, i - 1, a + 1, width, height, pass, cov);
                Search(array, i + 1, a - 1, width, height, pass, cov);
            }
        }


        private static Dictionary<string, WordType> _words = new Dictionary<string, WordType>(400000);
        private List<KeyValuePair<string, List<string>>> _found = new List<KeyValuePair<string, List<string>>>();

        private string[] FoundWords
        {
            get { return _found.Select(c => c.Key).Distinct().OrderByDescending(c => c.Length).ToArray(); }
        }
    }
}