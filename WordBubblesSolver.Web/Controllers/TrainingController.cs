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
using Microsoft.Ajax.Utilities;
using Newtonsoft.Json;
using WordBubblesSolver.Web.App;
using Image = System.Drawing.Image;
using Point = System.Drawing.Point;

namespace WordBubblesSolver.Web.Controllers
{
    

    public class TrainingController : Controller
    {
        // GET: Training
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult ShowData()
        {
            var trainingData = TrainingData.LoadTrainingData();
            const string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

            var sb = new StringBuilder();
            foreach (var letter in letters)
            {
                sb.AppendLine(letter + ": " + trainingData.Count(c => c.Letter.ToString() == letter.ToString()));
            }


            return Content(sb.ToString(), "text/plain");
        }

        
        [HttpPost]
        public ActionResult Process(HttpPostedFileBase file)
        {
            var model = new List<TrainingDataItem>();

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

            // get information about detected objects            
            var shapeChecker = new SimpleShapeChecker();

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
                    var base64 = (letter.ToBase64());
                    var sb = new StringBuilder();

                    var bwBitmap = new Bitmap(75, 75, PixelFormat.Format32bppArgb);


                    for (int y = 40; y < 115; y++)
                    {
                        for (int x = 40; x < 115; x++)
                        {
                            var color = letter.GetPixel(x, y);
                            if (color.Name == "ff000000")
                            {
                                sb.Append("0");
                                bwBitmap.SetPixel(x - 40, y - 40, Color.Black);
                            }
                            else
                            {
                                sb.Append("1");
                                bwBitmap.SetPixel(x - 40, y - 40, Color.White);
                            }
                        }

                        sb.AppendLine();
                    }

                    model.Add(new TrainingDataItem
                    {
                        Base64Image = (bwBitmap.ToBase64()),
                        Letter = TrainingData.GetBestGuess(bwBitmap)
                    });
                }
            }


            return View(model.ToArray());
        }

        [HttpPost]
        public ActionResult Save(TrainingDataItem[] items)
        {
            var trainingData = TrainingData.LoadTrainingData();

            var saveList = new List<TrainingDataItem>();

            foreach (var item in items)
            {
                item.Letter = item.Letter.ToUpper();
                if (trainingData.Count(c => c.Letter == item.Letter) < 100)
                    saveList.Add(item);
            }



            var trainingFile = Server.MapPath("~/App_data/TrainingData/" + Guid.NewGuid() + ".json");
            System.IO.File.WriteAllText(trainingFile, JsonConvert.SerializeObject(saveList.ToArray()));

            return Redirect("/Training/Index");
        }

        
        
    }   
}