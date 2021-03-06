﻿using HtmlAgilityPack;
using ExamRevisionHelper.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Globalization;

namespace ExamRevisionHelper.Sources
{
    public class PaperSourceGCEGuide : PaperSource
    {
        public PaperSourceGCEGuide()
        {
            Name = "gce_guide";
            DisplayName = "GCE Guide";
            UrlBase = "https://papers.gceguide.com/";
        }
        public PaperSourceGCEGuide(XmlDocument data) : base(data)
        {
            Name = "gce_guide";
            DisplayName = "GCE Guide";
            UrlBase = "https://papers.gceguide.com/";

            XmlNode subjListNode = data.SelectSingleNode("/Data/SubjectList");
            if (subjListNode == null) throw new Exception("Failed to load subject list.");

            XmlNodeList nodes = subjListNode.SelectNodes("/Data/SubjectList//Subject");
            foreach (XmlNode node in nodes)
            {
                Subject subj = new Subject
                {
                    Curriculum = node.ParentNode.Name == "IGCSE" ? Curriculums.IGCSE : Curriculums.ALevel,
                    Name = node.Attributes["Name"].Value,
                    SyllabusCode = node.Attributes["SyllabusCode"].Value,
                };
                SubjectUrlMap.Add(subj, node.Attributes["Url"].Value);
            }

            //Load cached repositories of subscribed subjects
            XmlNode subsNode = data.SelectSingleNode("/Data/Subscription");
            if (subsNode == null) throw new Exception("Failed to load subscription.");

            XmlNodeList subjNodes = subsNode.SelectNodes("/Data/Subscription/Subject");
            foreach (XmlNode subjectNode in subjNodes)
            {
                SubjectSubscriptionUtils.TryFindSubject(subjectNode.Attributes["SyllabusCode"].Value, out Subject subj, SubjectUrlMap.Keys);
                //if (!PastPaperHelperCore.SubscribedSubjects.Contains(subj)) continue;
                //TODO: Ignore data of subjects which are not subscribed

                PaperRepository repo = new PaperRepository(subj);
                foreach (XmlNode yearNode in subjectNode.ChildNodes)
                {
                    ExamYear year = new ExamYear { Year = yearNode.Attributes["Year"].Value };
                    if (yearNode.Attributes["Syllabus"] != null) year.Syllabus = new Syllabus { Year = year.Year, Url = yearNode.Attributes["Syllabus"].Value };

                    foreach (XmlNode examNode in yearNode.ChildNodes)
                    {
                        Exam exam = new Exam(examNode, repo.Subject);
                        switch (exam.Series)
                        {
                            case ExamSeries.Spring:
                                year.Spring = exam;
                                break;
                            case ExamSeries.Summer:
                                year.Summer = exam;
                                break;
                            case ExamSeries.Winter:
                                year.Winter = exam;
                                break;
                            default:
                                year.Specimen = exam;
                                break;
                        }
                    }
                    repo.Add(year);
                }

                repo.Sort(new Comparison<ExamYear>((a, b) => { return -a.CompareTo(b); }));//TODO: Allow user preference
                Subscription.Add(repo.Subject, repo);
            }

        }

        //TODO: scan all papers, then sort
        public override async Task<PaperRepository> GetPapersAsync(Subject subject) => await Task.Run(() =>
        {
            if (!SubjectUrlMap.ContainsKey(subject)) throw new Exception("Subject not supported, try reloading SubjectUrlMap.");

            PaperRepository repository = new PaperRepository(subject);
            Dictionary<Exam, List<Paper>> tmpRepo = new Dictionary<Exam, List<Paper>>();

            //Recursively process dir/files on the page
            ProcessPage(SubjectUrlMap[subject], repository, tmpRepo);

            //Sort by components
            foreach (KeyValuePair<Exam, List<Paper>> item in tmpRepo)
            {
                var components = from paper in item.Value
                                 group paper by paper.Component into component
                                 orderby component.Key ascending
                                 select new Component(component.Key, component.ToArray());
                item.Key.Components = components.ToArray();
            }

            repository.Sort(new Comparison<ExamYear>((a, b) => { return -a.CompareTo(b); }));

            return repository;
        }).ConfigureAwait(false);

        private void ProcessPage(string url, PaperRepository repository, Dictionary<Exam, List<Paper>> tmpRepo)
        {
            if (!url.EndsWith('/')) url += '/';
            HtmlDocument doc = web.Load(new Uri(url));
            HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//*[@id=\"paperslist\"][1]/li");

            foreach (HtmlNode node in nodes)
            {
                var nodeClass = node.Attributes["class"].Value;
                if (nodeClass == "file")
                {
                    ProcessFileNode(
                        fileName: node.ChildNodes[0].Attributes["href"].Value,
                        url: url,
                        repository: repository,
                        tmpRepo: tmpRepo);
                }
                else if (nodeClass == "dir")
                {
                    var folderName = node.ChildNodes[0].Attributes["href"].Value;
                    ProcessPage($"{url}{folderName}/", repository, tmpRepo);
                }
            }
        }

        private void ProcessFileNode(string fileName, string url, PaperRepository repository, Dictionary<Exam, List<Paper>> tmpRepo)
        {
            Subject subject = repository.Subject;

            string[] split = fileName.Substring(0, fileName.Length - 4).Split('_');

            if (split.Length > 4 || split.Length < 3 || fileName.Substring(0, 4) != subject.SyllabusCode) return;
            if (split[1].Length < 3) return;

            string yr = "20" + split[1].Substring(1, 2);
            ExamYear year = repository.GetExamYear(yr);
            if (year == null)
            {
                year = new ExamYear { Year = yr };
                repository.Add(year);
            }

            //Select an exsisting exam or create a new one
            Exam exam;
            switch (split[1][0])
            {
                default:
                    if (year.Specimen == null)
                    {
                        exam = new Exam
                        {
                            Year = yr,
                            Subject = subject,
                            Series = ExamSeries.Specimen
                        };
                        year.Specimen = exam;
                    }
                    else exam = year.Specimen; break;
                case 'm':
                    if (year.Spring == null)
                    {
                        exam = new Exam
                        {
                            Year = yr,
                            Subject = subject,
                            Series = ExamSeries.Spring
                        };
                        year.Spring = exam;
                    }
                    else exam = year.Spring; break;
                case 's':
                    if (year.Summer == null)
                    {
                        exam = new Exam
                        {
                            Year = yr,
                            Subject = subject,
                            Series = ExamSeries.Summer
                        };
                        year.Summer = exam;
                    }
                    else exam = year.Summer; break;
                case 'w':
                    if (year.Winter == null)
                    {
                        exam = new Exam
                        {
                            Year = yr,
                            Subject = subject,
                            Series = ExamSeries.Winter
                        };
                        year.Winter = exam;
                    }
                    else exam = year.Winter; break;
            }

            if (fileName.Contains("sy", StringComparison.Ordinal))
                year.Syllabus = new Syllabus { Url = url + fileName, Year = yr };
            else if (fileName.Contains("gt", StringComparison.Ordinal))
                exam.GradeThreshold = new GradeThreshold { Exam = exam, Url = url + fileName, };
            else if (fileName.Contains("er", StringComparison.Ordinal))
                exam.ExaminersReport = new ExaminersReport { Exam = exam, Url = url + fileName, };
            else
            {
                Paper paper = new Paper(fileName, exam, url + fileName);
                if (tmpRepo.ContainsKey(exam))
                    tmpRepo[exam].Add(paper);
                else
                    tmpRepo.Add(exam, new List<Paper> { paper });
            }
        }

        public override async Task<Dictionary<Subject, string>> GetSubjectUrlMapAsync(Curriculums curriculum) => await Task.Run(() =>
        {
            string url = UrlBase + curriculum switch
            {
                Curriculums.IGCSE => "IGCSE/",
                Curriculums.ALevel => "A%20Levels/",

                _ => throw new Exception($"Curriculum {curriculum.ToString()} not supported")
            };

            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(new Uri(url));
            HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes("//*[@id=\"paperslist\"][1]/li");

            Dictionary<Subject, string> result = new Dictionary<Subject, string>();
            foreach (HtmlNode node in nodes)
            {
                HtmlNode entry = node.ChildNodes[0];
                HtmlAttribute herf = entry.Attributes["href"];
                string code = entry.InnerText.Split(' ').Last();
                result.Add(new Subject
                {
                    Curriculum = curriculum,
                    Name = entry.InnerText.Substring(0, entry.InnerText.Length - 7),
                    SyllabusCode = code.Substring(1, 4)
                }, $"{url}{herf.Value}/");
            }
            return result;
        }).ConfigureAwait(false);
    }
}
