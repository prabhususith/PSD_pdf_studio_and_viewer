using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OmniPdf.App.Core
{
    public enum DiffType
    {
        Unchanged,
        Added,
        Deleted,
        Modified
    }

    public class DiffItem
    {
        public DiffType Type { get; set; }
        public string Text { get; set; } = "";
        public int PageNumber { get; set; }
    }

    public class DocumentComparisonResult
    {
        public string DocAPath { get; set; } = "";
        public string DocBPath { get; set; } = "";
        public int TotalPagesA { get; set; }
        public int TotalPagesB { get; set; }
        public int AdditionsCount { get; set; }
        public int DeletionsCount { get; set; }
        public int ModificationsCount { get; set; }
        public double SimilarityPercentage { get; set; }
        public List<DiffItem> Differences { get; set; } = new();
    }

    public static class PdfDiffService
    {
        public static DocumentComparisonResult CompareDocuments(string docAPath, string docBPath)
        {
            var result = new DocumentComparisonResult
            {
                DocAPath = docAPath,
                DocBPath = docBPath
            };

            if (!File.Exists(docAPath) || !File.Exists(docBPath)) return result;

            var docA = PdfDocumentModel.LoadFromFile(docAPath);
            var docB = PdfDocumentModel.LoadFromFile(docBPath);

            result.TotalPagesA = docA.Pages.Count;
            result.TotalPagesB = docB.Pages.Count;

            int maxPages = Math.Max(docA.Pages.Count, docB.Pages.Count);
            int totalWords = 0;
            int matchingWords = 0;

            for (int p = 0; p < maxPages; p++)
            {
                string textA = p < docA.Pages.Count ? docA.Pages[p].ContentText : "";
                string textB = p < docB.Pages.Count ? docB.Pages[p].ContentText : "";

                var linesA = textA.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var linesB = textB.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                var setA = new HashSet<string>(linesA, StringComparer.OrdinalIgnoreCase);
                var setB = new HashSet<string>(linesB, StringComparer.OrdinalIgnoreCase);

                foreach (var line in linesA)
                {
                    totalWords += line.Split(' ').Length;
                    if (!setB.Contains(line))
                    {
                        result.Differences.Add(new DiffItem
                        {
                            Type = DiffType.Deleted,
                            Text = line,
                            PageNumber = p + 1
                        });
                        result.DeletionsCount++;
                    }
                    else
                    {
                        matchingWords += line.Split(' ').Length;
                    }
                }

                foreach (var line in linesB)
                {
                    totalWords += line.Split(' ').Length;
                    if (!setA.Contains(line))
                    {
                        result.Differences.Add(new DiffItem
                        {
                            Type = DiffType.Added,
                            Text = line,
                            PageNumber = p + 1
                        });
                        result.AdditionsCount++;
                    }
                }
            }

            result.SimilarityPercentage = totalWords > 0 ? Math.Round((matchingWords * 2.0 / totalWords) * 100.0, 1) : 100.0;
            return result;
        }
    }
}
