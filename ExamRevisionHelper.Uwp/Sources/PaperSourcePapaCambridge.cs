﻿using HtmlAgilityPack;
using ExamRevisionHelper.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace ExamRevisionHelper.Sources
{
    class PaperSourcePapaCambridge : PaperSource
    {
        public PaperSourcePapaCambridge()
        {
            Name = "PapaCambridge";
            UrlBase = "https://papacambridge.com/";
        }
        public PaperSourcePapaCambridge(XmlDocument data) : base(data)
        {
            Name = "PapaCambridge";
            UrlBase = "https://papacambridge.com/";
        }

        public override async Task<PaperRepository> GetPapersAsync(Subject subject)
        {
            PaperRepository repo = new PaperRepository(subject);
            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(SubjectUrlMap[subject]);
            HtmlNodeCollection examNodes = doc.DocumentNode.SelectSingleNode("//table").SelectNodes("//td[@data-name and @data-href]");

            string resUrl1 = "", resUrl2 = ""; //specimen papers and syllabuses
            List<Exam> examList = new List<Exam>();
            foreach (HtmlNode examNode in examNodes)
            {
                string examUrl = "https://pastpapers.papacambridge.com/" + examNode.Attributes["data-href"].Value;
                string examCode = examNode.Attributes["data-name"].Value; ;

                if (examCode.Contains("&"))
                {
                    //find the folder storing specimen papers and syllabuses
                    if (string.IsNullOrEmpty(resUrl1)) resUrl1 = examUrl;
                    else resUrl2 = examUrl;
                    continue;
                }

                string substr = examCode.Substring(5);

                ExamSeries series;
                if (substr.Contains("Mar")) series = ExamSeries.Spring;
                else if (substr.Contains("Jun")) series = ExamSeries.Summer;
                else series = ExamSeries.Winter;

                Exam exam = new Exam
                {
                    Subject = subject,
                    Year = examCode.Substring(0, 4),
                    Series = series,
                };

                HtmlDocument examPage = web.Load(examUrl);
                HtmlNodeCollection paperNodes = examPage.DocumentNode.SelectSingleNode("//table").SelectNodes("//tbody//td[@data-name]");

                if (paperNodes.Count != 0 && paperNodes[0].Attributes["data-name"]?.Value == "..")
                {
                    paperNodes.RemoveAt(0);
                }
                else continue;

                List<Paper> paperList = new List<Paper>();
                foreach (HtmlNode paperNode in paperNodes)
                {
                    string fileName = paperNode.Attributes["data-name"].Value;
                    string fileUrl = "https://pastpapers.papacambridge.com/" + paperNode.Attributes["data-href"].Value;
                    if (fileName.Contains("gt"))
                        exam.GradeThreshold = new GradeThreshold { Exam = exam, Url = fileUrl };
                    else if (fileName.Contains("er"))
                        exam.ExaminersReport = new ExaminersReport { Exam = exam, Url = fileUrl };//Not available
                    else
                        paperList.Add(new Paper(fileName, exam, fileUrl));
                }

                var comps = paperList.GroupBy(p => p.Component).ToArray();
                exam.Components = new Component[comps.Count()];
                for (int i = 0; i < exam.Components.Length; i++)
                {
                    exam.Components[i] = new Component(comps[i].Key, comps[i].ToArray());
                }
                examList.Add(exam);
            }
            //TODO: read specimen papers and syllabus

            foreach(var year in examList.GroupBy(exam => exam.Year))
            {
                ExamYear yr = new ExamYear { Year = year.Key };
                foreach (Exam item in year)
                {
                    switch (item.Series)
                    {
                        case ExamSeries.Spring:
                            yr.Spring = item;
                            break;
                        case ExamSeries.Summer:
                            yr.Summer = item;
                            break;
                        case ExamSeries.Winter:
                            yr.Winter = item;
                            break;
                        case ExamSeries.Specimen:
                            yr.Specimen = item;
                            break;
                        default:
                            break;
                    }
                }
                repo.Add(yr);
            }
            repo.Sort();
            return repo;
        }

        public override async Task<Dictionary<Subject, string>> GetSubjectUrlMapAsync(Curriculums curriculum)
        {
            string url = UrlBase;
            switch (curriculum)
            {
                case Curriculums.IGCSE: url += "igcse-subjects/"; break;
                case Curriculums.ALevel: url += "a-and-as-level-subjects/"; break;
            }
            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(url);
            HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("/html/body/div[1]/div/p");

            Dictionary<Subject, string> map = new Dictionary<Subject, string>();

            foreach (HtmlNode node in nodes)
            {
                HtmlNode nameTag = node.ChildNodes.FindFirst("a");
                HtmlNode syCodeTag = node.ChildNodes.FindFirst("span");
                Subject subj = new Subject
                {
                    Curriculum = curriculum,
                    Name = nameTag.InnerText.Trim(),
                    SyllabusCode = syCodeTag.InnerHtml.Substring(1, 4)
                };
                map.Add(subj, nameTag.Attributes[0].Value);
            }

            return map;
        }
    }
}
