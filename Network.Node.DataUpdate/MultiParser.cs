using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using Microsoft.VisualBasic.FileIO;

namespace Network.Node.DataUpdate
{
    public class MultipartParser
    {
        private byte[] requestData;

        public bool Success { get; private set; }

        public DataSet BeforeAfter { get; private set; }

        private string CLLI { get; set; }

        public MultipartParser(Stream stream)
        {
            ParseCSV(stream, Encoding.UTF8);
        }

        public MultipartParser(Stream stream, Encoding encoding)
        {
            ParseCSV(stream, encoding);
        }

        private void ParseCSV(Stream stream, Encoding encoding)
        {
            string[] headerRow = null;

            var DataComponent = new SqlConnector();
            var masterTable = new DataTable();
            var updateTable = new DataTable();
            var unalteredTable = new DataTable();
            var alteredTable = new DataTable();
            var ColNameMapping = new Dictionary<string, int>();
            List<string> clliList = new List<string>();

            List<string> omitCols = new List<string>() { "BUILDING_CLLI_CODE" };

            unalteredTable.TableName = "unaltered";
            alteredTable.TableName = "altered";

            this.Success = false;

            // Read the stream into a byte array
            byte[] data = ToByteArray(stream);
            requestData = data;

            // Copy to a string for header/content parsing
            string[] content = encoding.GetString(data).Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (string line in content)
            {

                if (headerRow == null)
                {
                    var nameColsSplit = line.Split(new string[] { "||" }, StringSplitOptions.None);
                    alteredTable.TableName = nameColsSplit[0];
                    masterTable.TableName = nameColsSplit[0];
                    CLLI = "BUILDING_CLLI_CODE";
                    headerRow = nameColsSplit[1].Split(',');

                    DataComponent.LoadTable(masterTable);
                    updateTable = masterTable.Copy();
                    unalteredTable = masterTable.Clone();
                    alteredTable = masterTable.Clone();

                    for (int i = 0; i < headerRow.Length; i++)
                    {
                        ColNameMapping[headerRow[i]] = i;
                    }
                }
                else
                {
                    var csvParser = new TextFieldParser(NewStream(line));
                    csvParser.Delimiters = new string[] { "," };

                    while (!csvParser.EndOfData)
                    {
                        var addRow = false;
                        var newRow = alteredTable.NewRow();

                        var _line = csvParser.ReadFields();

                        if (_line.Length != headerRow.Length)
                        {
                            return;
                        }

                        var updateRow = updateTable.Select(string.Format("{0} = '{1}'", CLLI, _line[ColNameMapping[CLLI]]));
                        newRow.ItemArray = updateRow[0].ItemArray;

                        foreach (string colName in headerRow)
                        {
                            if (!omitCols.Contains(colName))
                            {
                                try
                                {
                                    //newRow[colName] = _line[ColNameMapping[colName]];
                                    updateRow[0][colName] = _line[ColNameMapping[colName]];
                                    //addRow = true;
                                    clliList.Add("\"" + _line[ColNameMapping[CLLI]] + "\"");
                                }
                                catch (Exception e) { }
                            }
                        }

                        //if (addRow)
                        //{
                        //    alteredTable.Rows.Add(newRow);
                        //}
                    }

                    alteredTable = updateTable.GetChanges(DataRowState.Modified);
                    Success = DataComponent.UpdateTable(updateTable) > 0 ? true : false;

                    if (Success)
                    {
                        BeforeAfter = GetBeforeAfter(masterTable, alteredTable, unalteredTable, clliList);
                    }

                }
            }
        }
        private Stream NewStream(string str)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(str);
            writer.Flush();
            stream.Position = 0;

            return stream;
        }

        private DataSet GetBeforeAfter(DataTable masterTable, DataTable alteredTable, DataTable unalteredTable, List<string> clliList)
        {
            var beforeAfter = new DataSet(); ;
            unalteredTable.TableName = "before";
            alteredTable.TableName = "after";
            beforeAfter.Tables.Add(unalteredTable);
            beforeAfter.Tables.Add(alteredTable);

            var inList = string.Join(",", clliList).Replace('"','\'');

            var masterRows = masterTable.Select(string.Format("{0} in ({1})", CLLI, inList));

            foreach (DataRow row in masterRows)
            {
                DataRow newRow = unalteredTable.NewRow();
                newRow.ItemArray = row.ItemArray;
                unalteredTable.Rows.Add(newRow);
            }

            return beforeAfter;
        }

        private int IndexOf(byte[] searchWithin, byte[] serachFor, int startIndex)
        {
            int index = 0;
            int startPos = Array.IndexOf(searchWithin, serachFor[0], startIndex);

            if (startPos != -1)
            {
                while ((startPos + index) < searchWithin.Length)
                {
                    if (searchWithin[startPos + index] == serachFor[index])
                    {
                        index++;
                        if (index == serachFor.Length)
                        {
                            return startPos;
                        }
                    }
                    else
                    {
                        startPos = Array.IndexOf<byte>(searchWithin, serachFor[0], startPos + index);
                        if (startPos == -1)
                        {
                            return -1;
                        }
                        index = 0;
                    }
                }
            }

            return -1;
        }

        private byte[] ToByteArray(Stream stream)
        {
            byte[] buffer = new byte[32768];
            using (MemoryStream ms = new MemoryStream())
            {
                while (true)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                    {
                        return ms.ToArray();
                    }

                    ms.Write(buffer, 0, read);
                }
            }
        }
    }
}
