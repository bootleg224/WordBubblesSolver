using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;


namespace WordBubblesSolver.Web.Controllers
{
    //Code sample modified from www.dotnetperls.com/word-search
    internal enum WordType : byte
    {
        FullWord,
        PartialWord,
        FullWordAndPartialWord
    }

    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }


        [HttpPost]
        public ActionResult Twilio(string Body)
        {
            var sb = SolvePuzzle(Body, 4, 10);
            return
                Content(
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\r\n<Response><Message>" +
                    sb.ToString().Substring(0, Math.Min(sb.Length, 1600)) + "</Message></Response>", "text/xml");
        }

        [HttpPost]
        public ActionResult Index(string data, int smallest = 3, int largest = 6)
        {
            var sb = SolvePuzzle(data, smallest, largest);
            return Content(sb.ToString(), "text/plain");
        }

        public StringBuilder SolvePuzzle(string data, int smallest, int largest)
        {
            data = data.ToLower();

            //load dictionary of words
            using (var reader = new StreamReader(Server.MapPath("~/App_Data/enable1.txt")))
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


            var sb = new StringBuilder();
            foreach (
                var k in
                    _found.Keys.Where(c => c.Length >= smallest && c.Length <= largest).OrderByDescending(k => k.Length)
                )
            {
                sb.AppendLine(k);
            }


            return sb;
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
                    // Don't display same word twice.
                    if (!_found.ContainsKey(pass))
                    {
                        _found.Add(pass, true);
                    }
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
        private Dictionary<string, bool> _found = new Dictionary<string, bool>();
    }
}