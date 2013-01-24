using System;
using System.Data;
using System.IO;
using System.Text;
using System.Collections.Generic; // List<T>
using Mono.Data.Sqlite;
using System.Xml;
using System.Xml.Linq;

// Eigene
using Tools;

namespace SQLite
{
	public class Client
	{
		private static SqliteConnection Connection;
		private static string DatabaseFile;

		// Constructor
		public Client(string ADatabaseFile)
		{
			DatabaseFile = ADatabaseFile;
			Open();
		}

		// Destructor
		~ Client ()
		{
			Close();
		}

		// Open
		private static void Open ()
		{
			if (! File.Exists (DatabaseFile)) {
				SqliteConnection.CreateFile (DatabaseFile);
			}
			Connection = new SqliteConnection ("Data Source=" + DatabaseFile);
			Connection.Open ();

			// Initial Connection Querys
			string[] InitSQL = {
				"PRAGMA journal_mode = WAL;",
				"PRAGMA encoding='UTF-8';",
				"PRAGMA auto_vacuum='INCREMENTAL';",
				"PRAGMA incremental_vacuum(10000);",
				"PRAGMA synchronous='FULL';"
			};
			foreach (string Query in InitSQL) {
				using (var Cmd = Connection.CreateCommand ()) {
					Cmd.CommandText = Query;
					Cmd.ExecuteNonQuery ();
				}
			}
		}

		// Close
		private static void Close ()
		{
			Connection.Close();
			Connection = null;
		}

		// ExecuteSQL
		public string ExecuteSQL (string ASQL, Boolean ANoResult = false)
		{
			try
			{
				using (var Cmd = Connection.CreateCommand ()) {

					// Query without result request by client
					if (ANoResult) {
						// Execute SQL-Query 
						Cmd.CommandText = ASQL;
						Cmd.ExecuteNonQuery();

						// Return no result
						return String.Empty;

					// Query WITH result request by client
					} else {
						// Execute SQL-Query 
						Cmd.CommandText = ASQL;
						var Reader = Cmd.ExecuteReader();

						// Create XML-Document
						XDocument XML = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));
						XElement XRoot = new XElement("Result");

						// Add Result
						int Row = 0;
						XElement XRows = new XElement("Rows");
						XElement XRow = null;
						XElement XField = null;
						XElement XFieldNames = new XElement("Fields");
						XElement XFieldName = null;
						bool first = true;
						while (Reader.Read ())
						{
							// On First element add Field/Column Information
							if (first) {
								first = false;
								for (int i = 0; i<Reader.FieldCount; i++) {
									XFieldName = new XElement("Col");
									XFieldName.Add(new XAttribute("No", i));
									XFieldName.Add(new XAttribute("Name", Reader.GetName(i)));
									XFieldNames.Add(XFieldName);
								}
							}

							// Add Row
							XRow = new XElement("Row");
							XRow.Add(new XAttribute("No", Row));
							for (int i = 0; i<Reader.FieldCount; i++) {
								XField = new XElement("Col");
								XField.Add(new XAttribute("No", i));
								XField.Add(new XAttribute("Type", Reader.GetFieldType(i).Name));
								XField.Add(new XAttribute("Value",  Reader[i].ToString()));
								XRow.Add(XField);
							}
							XRows.Add(XRow);
							Row += 1;
						}
						XRoot.Add(XFieldNames);
						XRoot.Add(XRows);

						// Add Status/Error and Query Information
						XElement XStatus = new XElement("Status");
						XStatus.Add(new XAttribute("Error", false));
						XStatus.Add(new XAttribute("FieldCount", Reader.FieldCount.ToString()));
						XStatus.Add(new XAttribute("RowCount", Row.ToString()));
						XRoot.AddFirst(XStatus);

						// Return XML-Document as String
						XML.Add(XRoot);
						return XML.Declaration.ToString() + Environment.NewLine + XML.ToString();
					}

				}
			}
			catch(Exception e)
			{
				// Create XML-Document
				XDocument XML = new XDocument(new XDeclaration("1.0", "utf-8", "yes"));
				XElement XRoot = new XElement("Result");

				// Add Error Message to XML-Document
				XElement XStatus = new XElement("Status");
				XStatus.Add(new XAttribute("Error", true));
				XStatus.Add(new XAttribute("ErrorMessage", e.Message.Replace("\r", " ").Replace("\n", " ").Replace("  ", " ").Trim()));
				XRoot.Add(XStatus);

				// Return XML-Document as String
				XML.Add(XRoot);
				return XML.Declaration.ToString() + Environment.NewLine + XML.ToString();
			}

		}
	}
}

