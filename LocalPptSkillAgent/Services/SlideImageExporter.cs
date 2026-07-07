using System;
using System.IO;
using LocalPptSkillAgent.Utils;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace LocalPptSkillAgent.Services
{
    public class SlideImageExporter
    {
        public string Export(PowerPoint.Slide slide)
        {
            var path = Path.Combine(FileStoreUtils.Exports, $"slide_{slide.SlideIndex}_{DateTime.Now:yyyyMMdd_HHmmssfff}.png");
            slide.Export(path, "PNG");
            return path;
        }
    }
}
