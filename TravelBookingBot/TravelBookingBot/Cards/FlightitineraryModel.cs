using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TravelBookingBot.Cards
{
    public class FlightitineraryModel
    {
        public Segment[] Segments { get; set; }
        public Bookingoption[] BookingOptions { get; set; }
        public Place[] Places { get; set; }
        public Carrier[] Carriers { get; set; }
        public Query Query { get; set; }
    }

    public class Query
    {
        public string Country { get; set; }
        public string Currency { get; set; }
        public string Locale { get; set; }
        public int Adults { get; set; }
        public int Children { get; set; }
        public int Infants { get; set; }
        public string OriginPlace { get; set; }
        public string DestinationPlace { get; set; }
        public string OutboundDate { get; set; }
        public string InboundDate { get; set; }
        public string LocationSchema { get; set; }
        public string CabinClass { get; set; }
        public bool GroupPricing { get; set; }
    }

    public class Segment
    {
        public int Id { get; set; }
        public int OriginStation { get; set; }
        public int DestinationStation { get; set; }
        public DateTime DepartureDateTime { get; set; }
        public DateTime ArrivalDateTime { get; set; }
        public int Carrier { get; set; }
        public int OperatingCarrier { get; set; }
        public int Duration { get; set; }
        public string FlightNumber { get; set; }
        public string JourneyMode { get; set; }
        public string Directionality { get; set; }
    }

    public class Bookingoption
    {
        public Bookingitem[] BookingItems { get; set; }
    }

    public class Bookingitem
    {
        public int AgentID { get; set; }
        public string Status { get; set; }
        public float Price { get; set; }
        public string Deeplink { get; set; }
        public int[] SegmentIds { get; set; }
    }

    public class Place
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public string Code { get; set; }
        public string Type { get; set; }
        public string Name { get; set; }
    }

    public class Carrier
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string ImageUrl { get; set; }
    }


}
