using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TLKAC_Printer_Upload_Service
{
    class TicketParser
    {
        public static Ticket Parse(FileStream file, FileInfo info)
        {
            try
            {
                file.Close();   //This is pretty bad, but whatever. It's not like this program is meant for serious professional use
                //It's bad because the concerns are leaking. Only people who walk through this program's execution carefully will know why this is here.
                //It's because the file is alredy open right now.
                //The state of the file is dependent on non-isolated places :(
                //Too lazy to rectify. Whatever.

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

                return new Ticket { OrderNumber = orderNumStr, OrderKind = kind };
            }
            catch (Exception e)
            {
                Service1.LogEvent("Something went wrong with the TicketParser: " + e.Message);
            }
            return null;
        }

        public void Hi()
        {

        }
    }

    class Ticket
    {
        public String OrderNumber { get; set; }
        public String OrderKind { get; set; }
    }
}
