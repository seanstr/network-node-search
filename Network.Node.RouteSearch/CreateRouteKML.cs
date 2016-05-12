using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Network.Node.Data;

namespace Network.Node.RouteSearch
{
    public class CreateRouteKML
    {
        private readonly IList<CABLES_V> _routeTable;
        private readonly IList<CABLE> _cables;
        private readonly string _kmldir;
        private const string KML_TEMPLATE = "KML_File_Template.xml";
        private const string ELEMENT = "element";

        private int MaxStrands { get; set; }

        private FileStream KMLTemplate { get; set; }

        private string StartCLLI { get; set; }

        private string EndCLLI { get; set; }

        private int RouteNumber { get; set;}


        public CreateRouteKML(IList<CABLES_V> routeTable, IList<CABLE> cables, string a_clli, string z_clli, string kmlDir)
        {
            MaxStrands = 0;
            StartCLLI = a_clli;
            EndCLLI = z_clli;
            _routeTable = routeTable;
            _cables = cables;
            _kmldir = kmlDir;
        }

        public void CreateKML()
        {
            CreateKML(CreateLines(_routeTable));
        }

        private List<KMLLine> CreateLines(IList<CABLES_V> routeTable)
        {
            var lines = new List<KMLLine>();
            MaxStrands = Convert.ToInt32(routeTable.Max(_ => _.AVAILABLE_STRANDS));
            //RouteNumber = Convert.ToInt32(routeTable.Compute("MAX(route_no)", string.Empty));

            foreach(var cable in routeTable)
            {
                var cableDescription = "<tr><td>Cable:</td><td>" + cable.CABLE_CLFI + "</td>";
                cableDescription += "<td>Strands:</td><td>" + cable.AVAILABLE_STRANDS + "</td></tr>";
                cableDescription += GetStrandsDescription(cable);

                cableDescription = "<table>" + cableDescription + "</table>";

                var line = new KMLLine {cable = cable, Description = cableDescription};

                lines.Add(line);
            }

            return lines;
        }

        private XmlNode CreateLine(XmlDocument doc, KMLLine line, KMLPlace startPlace, KMLPlace endPlace)
        {
            var placemarkNode = doc.CreateElement("Placemark");

            var lineName = line.cable.START_CUST_SITE_CLLI + " to " + line.cable.END_CUST_SITE_CLLI;
            placemarkNode.AppendChild(NewXMLNode(doc, ELEMENT, "name", lineName));

            var strandDesc = "<table><tr><td>Max Strands for " + StartCLLI.Trim() + " to " + EndCLLI.Trim() + ": " + MaxStrands + "</td></tr></table>";

            placemarkNode.AppendChild(NewXMLNode(doc, ELEMENT, "description", line.Description + strandDesc));
            placemarkNode.AppendChild(NewXMLNode(doc, ELEMENT, "styleUrl", "#msn_purple-stars0"));

            var lineStringNode = doc.CreateElement("LineString");

            lineStringNode.AppendChild(NewXMLNode(doc, ELEMENT, "tessellate", "1"));
            lineStringNode.AppendChild(NewXMLNode(doc, ELEMENT, "coordinates", startPlace.Longitude + "," + startPlace.Latitude + ",0 " + endPlace.Longitude + "," + endPlace.Latitude + ",0"));

            placemarkNode.AppendChild(lineStringNode);

            return placemarkNode;
        }

        private void CreateKML(IEnumerable<KMLLine> lines)
        {
            var folderName = "Route";

            var xmlDocument = new XmlDocument();
            
            try
            {
                    xmlDocument.Load(InitializeKML());
            }
            catch
            {
                return;
            }

            var docNode = xmlDocument.DocumentElement;
            var folderNode = xmlDocument.CreateElement("Folder");
            folderNode.AppendChild(NewXMLNode(xmlDocument, ELEMENT, "name", folderName));

            docNode.ChildNodes[docNode.ChildNodes.Count - 1].AppendChild(folderNode);

            foreach (KMLLine line in lines)
            {
                var startPlace = new KMLPlace(line, "start");
                var endPlace = new KMLPlace(line, "end");
                folderNode.AppendChild(CreatePlace(xmlDocument, startPlace));
                folderNode.AppendChild(CreatePlace(xmlDocument, endPlace));
                folderNode.AppendChild(CreateLine(xmlDocument, line, startPlace, endPlace));
            }

            SaveKML(xmlDocument);
        }

        private XmlNode NewXMLNode(XmlDocument xmlDocument, string nodeType, string name, string value)
        {
            var newElement = xmlDocument.CreateNode(nodeType, name, "");
            newElement.InnerText = value;

            return newElement;
        }

        private XmlNode NewXMLCDATANode(XmlDocument xmlDocument, string nodeType, string name, string value)
        {
            var newElement = xmlDocument.CreateNode(nodeType, name, "");
            var cdataElement = xmlDocument.CreateCDataSection(value);

            newElement.AppendChild(cdataElement);

            return newElement;
        }

        private XmlNode CreatePlace(XmlDocument doc, KMLPlace place)
        {
            var placemarkNode = doc.CreateElement("Placemark");

            var placemarkDesc = "<table><tr><td>Longitude: " + place.Longitude + "</td></tr><tr><td>Latitude: " + place.Latitude + "</td></tr></table>";

            placemarkNode.AppendChild(NewXMLNode(doc, ELEMENT, "name", place.Name));
            placemarkNode.AppendChild(NewXMLNode(doc, ELEMENT, "Snippet", ""));
            placemarkNode.AppendChild(NewXMLCDATANode(doc, ELEMENT, "description", placemarkDesc));

            var lookAtNode = doc.CreateElement("LookAt");

            lookAtNode.AppendChild(NewXMLNode(doc, ELEMENT, "longitude", place.Longitude));
            lookAtNode.AppendChild(NewXMLNode(doc, ELEMENT, "latitude", place.Latitude));
            lookAtNode.AppendChild(NewXMLNode(doc, ELEMENT, "altitude", "0"));
            lookAtNode.AppendChild(NewXMLNode(doc, ELEMENT, "range", "0.1"));
            lookAtNode.AppendChild(NewXMLNode(doc, ELEMENT, "tilt", "66"));
            lookAtNode.AppendChild(NewXMLNode(doc, ELEMENT, "heading", "0"));

            placemarkNode.AppendChild(lookAtNode);

            placemarkNode.AppendChild(NewXMLNode(doc, ELEMENT, "styleUrl", "#msn_arrow00"));

            var pointNode = doc.CreateElement("Point");

            pointNode.AppendChild(NewXMLNode(doc, ELEMENT, "coordinates", place.Longitude + ", " + place.Latitude + ",0"));
            placemarkNode.AppendChild(pointNode);

            return placemarkNode;
        }

        internal void logger(string message)
        {
            string logFile = @"e:\IIS-Logfiles\log.txt";
            System.IO.File.AppendAllText(logFile, DateTimeOffset.Now.ToString("u") + "  " + message + Environment.NewLine);
        }


        /// <summary>
        /// Gets the description (strand number, assignment) for each strand in a cable
        /// </summary>
        /// <param name="row">The cable_routes cable to get strand descriptions for</param>
        /// <returns>HTML "table" of descriptions for each strand in the cable</returns>
        private string GetStrandsDescription(CABLES_V row)
        {
            try
            {
                var strandsDescription = new StringBuilder("\r\n<tr><td>Strand Number</td><td>Assignment</td></tr>");
                var cable = _cables.First(_ => _.CABLE_CLFI == row.CABLE_CLFI);

                foreach (var strand in cable.STRANDS)
                {
                    strandsDescription.AppendFormat("\r\n<tr><td>{0}</td><td>{1}</td></tr>", strand.STRAND_NUMBER, strand.ASSIGNMENT);
                }
                return strandsDescription.ToString();
            }

            catch (Exception e)
            {
                logger(string.Format("Exception caught = {0}", e));
            }
            return "\r\n<tr><td colspan='2'>error retrieving strands</td></tr>";
        }

        private FileStream InitializeKML()
        {
            var kmlTemplatePath = Path.Combine(_kmldir, KML_TEMPLATE);
            if (File.Exists(kmlTemplatePath))
            {
                try
                {
                    KMLTemplate = File.OpenRead(kmlTemplatePath);
                }
                catch (Exception e)
                {
                    return null;
                }
            }

            return KMLTemplate;
        }

        private void SaveKML(XmlDocument doc)
        {
            //doc.Save(_kmldir + StartCLLI + "-" + EndCLLI + "-Route" + RouteNumber + ".kml");
            doc.Save(Path.Combine(_kmldir, StartCLLI + "-" + EndCLLI + ".kml"));
        }
    }
}
