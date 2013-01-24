using System;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace TCP
{
	internal class Client
	{
		private string Host = "localhost";
		private int Port = 11833;
		private TcpClient Connection = null;
		private NetworkStream Stream = null; 

		// Constructor
		public Client(string AHost, int APort)
		{
			// Store Variables
			Host = AHost;
			Port = APort;
		}
		
		// Destructor
		~ Client ()
		{
			Stream = null;
			Connection = null;
		}
		
		// Connect
		public Boolean Connect ()
		{
			// Connect
			if (Connection != null) return false;
			try {
				Connection = new TcpClient(Host, Port);
				Stream = Connection.GetStream();

				StreamReader inStream = new StreamReader (Stream);
				StreamWriter outStream = new StreamWriter (Stream);

				string welcomeMsg = inStream.ReadLine();
				if (welcomeMsg != "SQLiteServer v1.0") {
					throw new System.NotSupportedException("Wrong version detected: " + welcomeMsg);
				}

				return true;
			} catch {
				Connection = null;
				Stream = null;
				return false;
			}
		}
		
		// Connected
		public Boolean Connected ()
		{
			if (Connection == null) return false;

			return Connection.Connected;
		}
		
		// Disconnect
		public Boolean Disconnect ()
		{
			if (Connection != null) {
				try
				{
					Stream.Close ();
					Connection.Close ();   
					Connection = null;
				}
				catch
				{
					return false;
				}
			}

			// Rückgabe
			return true;
		}
		
		// ExecSQL
		public string ExecSQL (string ASQLQuery, Boolean ANoResult = false)
		{
			try
			{
				StreamReader inStream = new StreamReader (Stream);
				StreamWriter outStream = new StreamWriter (Stream);

				// Communication
			
 				// Protocol:
				// Client: REQUEST:3:1      <- Where 3 is Number of Lines following and 1 means no result (0 = with result)
				// Client: .SELECT          <- Following 3 Lines are SQL Query-Lines prefixed by "."
				// Client: .*
				// Client: .FROM test;
				// (3 Lines Reached -> OnDataEvent fired within Server)
				// Server: RESULT:10        <- Where 10 is Number of Lines following
				// Server: .<xml...         <- Following 10 Lines is the XML-Result of the Query
				// (10 Lines Reached -> Client Parses Result)

				// Request senden
				outStream.WriteLine("REQUEST:" + ASQLQuery.Split('\n').Length + ":" + (ANoResult ? "1" : "0"));
				foreach (string Line in ASQLQuery.Split('\n')) {
					outStream.WriteLine("." + Line);
				}
				outStream.Flush ();

				// Only wait if Result is desired!
				if (ANoResult == false) {
					// Wait on result
					string RecvStr = "";
					string RecvLine = "";
					int Count = -1;
					bool RecvDone = false;
					Int64 LastLine = System.DateTime.Now.Ticks;
					while (true) {
						if ((System.DateTime.Now.Ticks-LastLine > 10000000)) { // 1sec since last line (timeout!)
							throw new System.TimeoutException("SQL query timed out");
						}
						if ((RecvLine = inStream.ReadLine()) != null) {
							if (RecvLine != "") LastLine = System.DateTime.Now.Ticks;
							if (RecvLine == "") {
								//

							// RESULT
							} else if (RecvLine.Split(':').GetValue(0).ToString().ToUpper() == "RESULT") {
								Count = Convert.ToInt32( RecvLine.Split(':').GetValue(1).ToString() );
								RecvDone = false;
								// .
							} else if ((!RecvDone) && (Count>0) && (RecvLine.Substring(0,1) == ".")) {
								Count = Count - 1;
								RecvStr = RecvStr + RecvLine.Substring(1,RecvLine.Length-1) + Environment.NewLine;
								if (Count == 0) {
									RecvStr = RecvStr.TrimEnd('\r', '\n');
									RecvDone = true;
									Count = -1;
								}
							}
							if (RecvDone) {
								return RecvStr;
							}
						}
					}
				} else {
					return String.Empty;
				}

			} catch (Exception e) {
				Disconnect();

				// Create XML error Document
				XDocument XML = new XDocument (new XDeclaration ("1.0", "utf-8", "yes"));
				XElement XRoot = new XElement ("Result");
				
				// Add error message to XML Document
				XElement XStatus = new XElement ("Status");
				XStatus.Add (new XAttribute ("Error", true));
				XStatus.Add (new XAttribute ("ErrorMessage", "Client Exception: " + e.Message));
				XRoot.Add (XStatus);
				
				// Return XML-Document as String
				XML.Add (XRoot);
				return XML.Declaration.ToString () + Environment.NewLine + XML.ToString ();
			}
		}

	}
}

