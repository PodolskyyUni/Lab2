using System;
using System.Xml.Linq;
using System.Linq;
using Microsoft.Maui.Controls;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Xsl;
using Microsoft.Extensions.Logging;

namespace Lab2
{
    public interface IXmlProcessingStrategy
    {
        IEnumerable<string> ProcessXml(XDocument xmlDocument, string selectedAttribute);
    }

    public class LinqToXmlStrategy : IXmlProcessingStrategy
    {
        public IEnumerable<string> ProcessXml(XDocument xmlDocument, string selectedAttribute)
        {
            return xmlDocument.Descendants("scientist")
                              .Where(x => x.Element(selectedAttribute) != null)
                              .Select(x => x.Element(selectedAttribute).Value);
        }
    }

    public class DomXmlStrategy : IXmlProcessingStrategy
    {
        public IEnumerable<string> ProcessXml(XDocument xmlDocument, string selectedAttribute)
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlDocument.ToString());
            var results = new List<string>();

            var nodes = xmlDoc.GetElementsByTagName("scientist");
            foreach (XmlNode node in nodes)
            {
                if (node[selectedAttribute] != null)
                {
                    results.Add(node[selectedAttribute].InnerText);
                }
            }

            return results;
        }
    }

    public class SaxXmlStrategy : IXmlProcessingStrategy
    {
        public IEnumerable<string> ProcessXml(XDocument xmlDocument, string selectedAttribute)
        {
            var results = new List<string>();
            using (var reader = xmlDocument.CreateReader())
            {
                while (reader.Read())
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "scientist")
                    {
                        if (reader.MoveToAttribute(selectedAttribute))
                        {
                            results.Add(reader.Value);
                        }
                    }
                }
            }

            return results;
        }
    }
    public class Scientist
    {
        public string Name { get; set; }
        public string Department { get; set; }
        public string Chair { get; set; }
        public string Degree { get; set; }
        public string DegreeDate { get; set; }
        public string Title { get; set; }
        public string TitleDate { get; set; }
        public string Details =>
            $"Name: {Name}\nDepartment: {Department}\nChair: {Chair}\nDegree: {Degree} (Date: {DegreeDate})\nTitle: {Title} (Date: {TitleDate})";
    }

    public partial class MainPage : ContentPage
    {
        private List<Scientist> _savedScientists = new List<Scientist>();
        private XDocument _xmlDocument;
        private IXmlProcessingStrategy _currentStrategy;
        private Dictionary<string, IXmlProcessingStrategy> _strategies;
        private static readonly string HtmlFilePath = "C:\\Users\\Admin\\Desktop\\transformed.html";
        private static readonly string XmlFilePath = "C:\\Users\\Admin\\Desktop\\scientists.xml";

        public MainPage()
        {
            InitializeComponent();

            _strategies = new Dictionary<string, IXmlProcessingStrategy>
    {
        { "LINQ to XML", new LinqToXmlStrategy() },
        { "SAX", new SaxXmlStrategy() },
        { "DOM", new DomXmlStrategy() }
    };

            _currentStrategy = _strategies["LINQ to XML"];

            LoadXmlFile();

            foreach (var strategyName in _strategies.Keys)
            {
                strategyPicker.Items.Add(strategyName);
            }
            strategyPicker.SelectedIndex = 0;
        }
        private string TransformXmlToHtml(string xmlFilePath, string xslPath)
        {
            try
            {
                XDocument xmlDocument = XDocument.Load(xmlFilePath);

                XslCompiledTransform transform = new XslCompiledTransform();
                transform.Load(xslPath);

                using (StringWriter writer = new StringWriter())
                {
                    transform.Transform(xmlDocument.CreateReader(), null, writer);
                    return writer.ToString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in XML to HTML transformation: {ex.Message}");
                return string.Empty;
            }
        }

        private XDocument CreateXmlFromSavedScientists(List<Scientist> scientists)
        {
            var xmlDocument = new XDocument(
                new XElement("scientists",
                    scientists.Select(s => new XElement("scientist",
                        new XElement("name", s.Name),
                        new XElement("department", s.Department),
                        new XElement("chair", s.Chair),
                        new XElement("degree", new XAttribute("date", s.DegreeDate), s.Degree),
                        new XElement("title", new XAttribute("date", s.TitleDate), s.Title)
                    ))
                )
            );

            return xmlDocument;
        }
        private void SaveXmlToFile(XDocument xmlDocument, string filePath)
        {
            xmlDocument.Save(filePath);
        }



        private List<Scientist> GetAllScientists()
        {
            return _xmlDocument.Descendants("scientist")
                               .Select(x => new Scientist
                               {
                                   Name = x.Element("name")?.Value,
                                   Department = x.Element("department")?.Value,
                                   Chair = x.Element("chair")?.Value,
                                   Degree = x.Element("degree")?.Value,
                                   DegreeDate = x.Element("degree")?.Attribute("date")?.Value,
                                   Title = x.Element("title")?.Value,
                                   TitleDate = x.Element("title")?.Attribute("date")?.Value
                               })
                               .ToList();
        }
        private void LoadXmlFile()
        {
            try
            {
                _xmlDocument = XDocument.Load(XmlFilePath);
                UpdatePicker();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading XML file: {ex.Message}");
                DisplayAlert("Error", $"Failed to load XML file: {ex.Message}", "OK");
            }
        }


        private void UpdatePicker()
        {
            if (_xmlDocument != null)
            {
                attributePicker.Items.Clear();

                // Включаємо "DegreeDate" і "TitleDate" безпосередньо у вираз LINQ
                var attributes = _xmlDocument.Descendants("scientist").Elements()
                                             .Select(x => x.Name.LocalName)
                                             .Distinct()
                                             .Concat(new[] { "DegreeDate", "TitleDate" })
                                             .ToList();

                foreach (var attr in attributes)
                {
                    attributePicker.Items.Add(attr);
                }
            }
        }

        private void OnSearchClicked(object sender, EventArgs e)
        {
            if (_xmlDocument == null || attributePicker.SelectedIndex == -1)
            {
                return;
            }

            var selectedAttribute = attributePicker.Items[attributePicker.SelectedIndex];
            var searchPhrase = searchEntry.Text?.ToLower() ?? "";

            // Use saved scientists if available; otherwise, fetch all scientists
            var scientists = _savedScientists.Any() ? _savedScientists : GetAllScientists();

            if (!string.IsNullOrWhiteSpace(searchPhrase))
            {
                scientists = scientists.Where(s =>
                {
                    switch (selectedAttribute.ToLower())
                    {
                        case "name":
                            return s.Name?.ToLower().Contains(searchPhrase) == true;
                        case "department":
                            return s.Department?.ToLower().Contains(searchPhrase) == true;
                        case "chair":
                            return s.Chair?.ToLower().Contains(searchPhrase) == true;
                        case "degree":
                            return s.Degree?.ToLower().Contains(searchPhrase) == true;
                        case "degreedate":
                            return s.DegreeDate?.ToLower().Contains(searchPhrase) == true;
                        case "title":
                            return s.Title?.ToLower().Contains(searchPhrase) == true;
                        case "titledate":
                            return s.TitleDate?.ToLower().Contains(searchPhrase) == true;
                        default:
                            return false;
                    }
                }).ToList();
            }
            logLabel.Text = $"Found {scientists.Count} results";
            resultsListView.ItemsSource = scientists;
        }

        private void OnSaveClicked(object sender, EventArgs e)
        {
            // Define the path for the SavedScientists.xml file
            string savedXmlPath = "C:\\Users\\Admin\\Desktop\\SavedScientists.xml";

            // Update _savedScientists with the currently displayed results
            _savedScientists = resultsListView.ItemsSource as List<Scientist>;

            if (_savedScientists != null && _savedScientists.Any())
            {
                // If the file exists, delete it
                if (File.Exists(savedXmlPath))
                {
                    File.Delete(savedXmlPath);
                }

                // Generate new XML from the saved scientists list
                var xmlDocument = CreateXmlFromSavedScientists(_savedScientists);

                // Save this new XML to file
                SaveXmlToFile(xmlDocument, savedXmlPath);

                DisplayAlert("Success", "Results saved successfully.", "OK");
            }
            else
            {
                DisplayAlert("Info", "No results to save.", "OK");
            }
        }

        private void OnClearClicked(object sender, EventArgs e)
        {
            resultLabel.Text = string.Empty;
            attributePicker.SelectedIndex = -1;
            _savedScientists.Clear();
            resultsListView.ItemsSource = null;
        }

        public void SetProcessingStrategy(IXmlProcessingStrategy strategy)
        {
            _currentStrategy = strategy;
        }

        private async void OnTransformButtonClicked(object sender, EventArgs e)
        {
            string xslPath = "C:\\Users\\Admin\\Desktop\\scientists.xsl";
            string savedXmlPath = "C:\\Users\\Admin\\Desktop\\SavedScientists.xml";

            try
            {
                if (_savedScientists.Any())
                {
                    var xmlDocument = CreateXmlFromSavedScientists(_savedScientists);
                    SaveXmlToFile(xmlDocument, savedXmlPath);

                    string htmlContent = TransformXmlToHtml(savedXmlPath, xslPath);

                    if (!string.IsNullOrEmpty(htmlContent))
                    {
                        SaveHtmlToFile(htmlContent, HtmlFilePath);
                        await DisplayAlert("Success", $"HTML saved to {HtmlFilePath}", "OK");
                    }
                    else
                    {
                        await DisplayAlert("Error", "Failed to generate HTML content.", "OK");
                    }
                }
                else
                {
                    await DisplayAlert("Info", "No saved scientists to transform.", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"An error occurred: {ex.Message}", "OK");
            }
        }



        private void SaveHtmlToFile(string htmlContent, string filePath)
        {
            File.WriteAllText(filePath, htmlContent);
        }

        private async void OnItemSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (e.SelectedItem is Scientist selectedScientist)
            {
                await DisplayAlert("Scientist Details", selectedScientist.Details, "OK");
            }
        }



        private void StrategyPicker_SelectedIndexChanged(object sender, EventArgs e)
        {
            var selectedStrategy = strategyPicker.Items[strategyPicker.SelectedIndex];
            _currentStrategy = _strategies[selectedStrategy];
        }


        private async void OnExitButtonClicked(object sender, EventArgs e)
        {
            bool answer = await DisplayAlert("Exit Programme", "Do you really want to exit the programme?", "Yes", "No");
            if (answer)
            {
                System.Diagnostics.Process.GetCurrentProcess().Kill();
            }
        }


    }
}