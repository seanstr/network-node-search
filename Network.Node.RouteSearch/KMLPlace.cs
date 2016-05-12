namespace Network.Node.RouteSearch
{
    public class KMLPlace
    {
        public string Name { get; set; }

        public string Longitude { get; set; }

        public string Latitude { get; set; }

        public string Altitude { get; set; }

        public string Range { get; set; }

        public string Tilt { get; set; }

        public string Heading { get; set; }

        public KMLPlace(KMLLine line, string point)
        {
            if (point == "start")
            {
                Name = line.cable.START_CUST_SITE_CLLI;
                Longitude = line.cable.start_longitude.ToString();
                Latitude = line.cable.start_latitude.ToString();
            }
            else
            {
                Name = line.cable.END_CUST_SITE_CLLI;
                Longitude = line.cable.end_longitude.ToString().Trim();
                Latitude = line.cable.end_latitude.ToString().Trim();
            }
        }
    }
}
