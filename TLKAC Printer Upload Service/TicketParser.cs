using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TLKAC_Printer_Upload_Service
{
    class TicketParser
    {
        public static Ticket Parse(FileStream file, FileInfo info)
        {
            try
            {
                //Read it all
                var ticketRaw = File.ReadAllText(info.FullName);

                //Extract the line with the order number and the order type
                var delim = "Order #: ";
                var delimStart = ticketRaw.IndexOf(delim);
                var delimEnd = ticketRaw.IndexOf("\n", delimStart);
                var rawInfo = ticketRaw.Substring(delimStart + delim.Length, delimEnd - delimStart - delim.Length);

                //Extract the order number
                var firstSpace = rawInfo.IndexOf(" ");
                var orderNumStr = rawInfo.Substring(0, firstSpace);

                //Extract order kind
                var excludeOrderNum = rawInfo.Substring(firstSpace, rawInfo.Length - firstSpace);
                var kind = excludeOrderNum.Trim(' ', '\r', '\n').Replace(" ", "");

                //Extract the date
                DateTime? date = null;
                try
                {
                    var dateRegX = new Regex(@"\d+\/\d+\/\d+ \d+:\d+:\d+ (AM|PM)");
                    var match = dateRegX.Match(ticketRaw);
                    date = DateTime.Parse(match.ToString());
                }
                catch (Exception e)
                {
                    Service1.LogEvent("TicketParserError: " + e.Message);
                }
                return new Ticket { OrderNumber = orderNumStr, OrderKind = kind, Timestamp = date };
            }
            catch (Exception e)
            {
                Service1.LogEvent("TicketParser Error: " + e.Message);
            }
            return null;
        }
    }

    class Ticket
    {
        public String OrderNumber { get; set; }
        public String OrderKind { get; set; }
        public DateTime? Timestamp { get; set; }
    }
}
