using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Web;
using AForge.Imaging;
using Newtonsoft.Json;

namespace WordBubblesSolver.Web.App
{
    public class TrainingDataItem
    {
        public string Letter { get; set; }
        public string Base64Image { get; set; }
    }

    public class BestGuess : TrainingDataItem
    {
        public Bitmap Bitmap { get; set; }
        public float Match { get; set; }
    }

    public static class TrainingData
    {
        //Can optimize this method by caching it
        public static List<TrainingDataItem> LoadTrainingData()
        {
            var trainingData = new List<TrainingDataItem>();

            foreach (var file in System.IO.Directory.GetFiles(HttpContext.Current.Server.MapPath("~/App_data/TrainingData/")))
            {
                var text = System.IO.File.ReadAllText(file);
                trainingData.AddRange(JsonConvert.DeserializeObject<TrainingDataItem[]>(text));
            }

            return trainingData;
        }

        //Can prob do this in parallel to speed it up
        public static string GetBestGuess(Bitmap image)
        {
            var sampleData = PreloadGuessObjects();

            var tm = new ExhaustiveTemplateMatching(0);

            foreach (var s in sampleData)
            {

                // compare two images
                TemplateMatch[] matchings = tm.ProcessImage(image.ConvertToFormat(PixelFormat.Format24bppRgb),
                    s.Bitmap.ConvertToFormat(PixelFormat.Format24bppRgb));
                // check similarity level
                s.Match = matchings[0].Similarity;
            }

            return sampleData.OrderByDescending(s => s.Match).First().Letter;
        }

        public static BestGuess[] PreloadGuessObjects()
        {
            var trainingData = LoadTrainingData();

            return trainingData.Select(t => new BestGuess
            {
                Letter = t.Letter, Bitmap = (Bitmap) (t.Base64Image.FromBase64())
            }).ToArray();
        }        
    }
}