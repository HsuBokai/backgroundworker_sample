using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Threading;

namespace digiEAR_api_sample
{
    public partial class Form1 : Form
    {
        DigiEarIndex.DEindex index_obj = new DigiEarIndex.DEindex();
        DigiEarSearch.DEsearch search_obj = new DigiEarSearch.DEsearch();

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (this.folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                this.media_path.Text = this.folderBrowserDialog1.SelectedPath;
            }   
        }

        private bool isMediaExtention(FileInfo fi)
        {
            return fi.Extension == ".wmv" || fi.Extension == ".wav" || fi.Extension == ".mp3" || fi.Extension == ".wma";
        }

        private void index(string dir_path, bool isReindex)
        {
            List<object> arguments = new List<object>();
            arguments.Add(dir_path);
            arguments.Add(isReindex);
            if (backgroundWorker1.IsBusy == false) backgroundWorker1.RunWorkerAsync(arguments);
        }

        private void add_pat_file(DataTable patDT, string patFilePath)
        {
            DataRow workRow = patDT.NewRow();
            workRow["Pat"] = patFilePath;
            patDT.Rows.Add(workRow);
        }

        private void add_term(DataTable queryDT, int minScore, string term)
        {
            DataRow workRow = queryDT.NewRow();
            workRow["MinScore"] = minScore;
            workRow["QueryTerm"] = term;
            queryDT.Rows.Add(workRow);
        }

        private void select_term_file_Click(object sender, EventArgs e)
        {
            if (this.openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                this.term_file.Text = this.openFileDialog1.FileName;
            }
        }

        private void search_btn_Click(object sender, EventArgs e)
        {
            if (!Directory.Exists(this.media_path.Text)) return;
            if (!File.Exists(this.term_file.Text)) return;
            if (!Directory.Exists(this.output_path.Text)) return;

            if (0 > index_obj.digiEAR_indexAllowed()) return;
            this.listBox1.Items.Add("index_allow_ok");
            
            string dir_path = this.media_path.Text;
            bool isReindex = false;

            List<object> arguments = new List<object>();
            arguments.Add(dir_path);
            arguments.Add(isReindex);
            if (backgroundWorker1.IsBusy == false) backgroundWorker1.RunWorkerAsync(arguments);
        }

        private void search()
        {
            //
            // searchDS is part of input
            // hitDS is the result of "search"
            //
            DataSet searchDS = new DataSet("SearchDS");
            DataSet hitDS = new DataSet("HitDS");
            int hSearch = 0;

            try
            {
                //
                // prepare DataTable patDT
                //
                DataTable patDT = searchDS.Tables.Add("PatDT");
                patDT.Columns.Add("Pat", typeof(string));
                DirectoryInfo di = new DirectoryInfo(this.media_path.Text);
                foreach (var fi in di.EnumerateFiles())
                {
                    if (isMediaExtention(fi))
                    {
                        string patFilePath = fi.FullName + ".pat";
                        add_pat_file(patDT, patFilePath);
                    }
                }

                //
                // prepare DataTable queryDT
                //
                DataTable queryDT = searchDS.Tables.Add("QueryDT");
                queryDT.Columns.Add("MinScore", typeof(int));
                queryDT.Columns.Add("QueryTerm", typeof(string));
                String[] terms = File.ReadAllText(this.term_file.Text).Split('\n');
                foreach (var term in terms)
                {
                    //Console.WriteLine("{0}", term);
                    char[] separators = { ',', '"' };
                    String[] components = term.Split(separators, StringSplitOptions.RemoveEmptyEntries);
                    if (components.Length >= 2)
                    {
                        add_term(queryDT, Convert.ToInt32(components[1]), components[0]);
                    }
                }

                //
                // max. # of hits to be returned from server: maxResults
                //
                int maxResults = 100;

                //
                // get a "handle" (hSearch) from server for "search"
                //
                DigiEarSearch.DEERRORCODE_TAG deError = search_obj.digiEAR_searchOpenHandle(out hSearch);
                if (deError != DigiEarSearch.DEERRORCODE_TAG.DE_SUCCESS) throw (new ApplicationException("digiEAR_searchOpenHandle :" + deError.ToString()));
                if (0 == hSearch) throw (new ApplicationException("Unable to get handle: " + deError));

                //
                // request Web Service
                //
                hitDS = search_obj.digiEAR_search2(maxResults, searchDS, hSearch);
                //
                // close the handle
                //
                deError = search_obj.digiEAR_searchCloseHandle(hSearch);
                if (deError != DigiEarSearch.DEERRORCODE_TAG.DE_SUCCESS) throw (new ApplicationException("digiEAR_searchCloseHandle :" + deError.ToString()));
                this.listBox1.Items.Add("Status: " + deError.ToString());



                result_grid_view.DataSource = hitDS.Tables["HitDT"];

                string output = string.Empty;
                output += "key word, media file name, starting time, ending time, score \n";
                if (0 < hitDS.Tables.Count)
                {
                    for (int i = 0; i < hitDS.Tables["HitDT"].Rows.Count; ++i)
                    {

                        output += hitDS.Tables["HitDT"].Rows[i][4].ToString() + ", ";
                        string pat_name = hitDS.Tables["HitDT"].Rows[i][1].ToString();
                        output += pat_name.Substring(0, pat_name.Length - 4) + ", ";
                        output += hitDS.Tables["HitDT"].Rows[i][2].ToString() + ", ";
                        output += hitDS.Tables["HitDT"].Rows[i][3].ToString() + ", ";
                        output += hitDS.Tables["HitDT"].Rows[i][0].ToString() + "\n";

                        /*
                        for (int j = 0; j < hitDS.Tables["HitDT"].Columns.Count; ++j)
                        {
                            output += hitDS.Tables["HitDT"].Rows[i][j].ToString();
                            output += ",";
                        }
                        output += "\n";                        
                         * */

                    }
                }
                else
                {
                    this.listBox1.Items.Add("No Search Results");
                }
                
                File.WriteAllText(this.output_path.Text + "/result.csv", output);
            }
            catch (Exception e1)
            {
                //txbxMessage.Text = e1.Message;
                this.listBox1.Items.Add(e1.Message);
                //if (0 < hSearch) ws.digiEAR_searchCloseHandle(hSearch);
            }

            //
            // dispose DataSet's
            //
            searchDS.Dispose();
            hitDS.Dispose();
            //ws.Dispose();
        }

        private void select_output_path_Click(object sender, EventArgs e)
        {
            if (this.folderBrowserDialog2.ShowDialog() == DialogResult.OK)
            {
                this.output_path.Text = this.folderBrowserDialog2.SelectedPath;
            }
        }

        private void clear_list_box_Click(object sender, EventArgs e)
        {
            this.listBox1.Items.Clear();
        }

        private void login_btn_Click(object sender, EventArgs e)
        {
            string site = this.serverSite_tb.Text;
            string userName = this.userName_tb.Text;
            string password = this.Password_tb.Text;

            index_obj.Url = index_obj.Url.Replace("//localhost/", "//" + site + "/");
            CredentialCache cache = new CredentialCache();
            cache.Add(new Uri(index_obj.Url), "Negotiate", new NetworkCredential(userName, password, site));
            index_obj.Credentials = cache;

            bool isLogin = (index_obj.digiEAR_indexAllowed() >= 0);
            if (isLogin)
            {
                MessageBox.Show("login success !!");

                search_obj.Url = search_obj.Url.Replace("//localhost/", "//" + site + "/");
                search_obj.Credentials = cache;

                search_btn.Enabled = true;
            }
            else
            {
                MessageBox.Show("login fail !!");
                return;
            }
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            List<object> genericlist = e.Argument as List<object>;
            string dir_path = (string)genericlist[0];
            bool isReindex = (bool)genericlist[1];

            int hIndex = 0;
            try
            {
                //
                // get a “indexing handle”(hIndex) for given acoustic model
                // from server for "indexing"
                //
                string acoName = "Aco_Tele_NAEnglish";
                DigiEarIndex.DEERRORCODE_TAG iError = index_obj.digiEAR_indexOpenHandle(acoName, out hIndex);
                //DigiEarIndex.DEERRORCODE_TAG iError = wi.digiEAR_indexOpenHandle2(2002, out hIndex);
                if (iError != DigiEarIndex.DEERRORCODE_TAG.DE_SUCCESS) throw (new ApplicationException("digiEAR_indexOpenHandle" + iError.ToString()));
                if (0 == hIndex) throw (new ApplicationException("Unable to get handle: " + iError));
                //this.listBox1.Items.Add("hIndex = " + hIndex.ToString());
                backgroundWorker1.ReportProgress(hIndex);

                //
                // Start indexing for the given media file, acoustic model
                // the .pat file and media file will be moved to patDir after
                // indexing
                //
                string patDir = dir_path;
                //this.listBox1.Items.Add("patDir = " + patDir);
                DirectoryInfo di = new DirectoryInfo(dir_path);
                foreach (var fi in di.EnumerateFiles())
                {
                    if (isMediaExtention(fi))
                    {
                        string patFilePath = fi.FullName + ".pat";
                        if (isReindex || !File.Exists(patFilePath))
                        {
                            string medFile = dir_path + "\\" + fi.Name;
                            iError = index_obj.digiEAR_index(medFile, acoName, patDir, hIndex);
                            if (iError != DigiEarIndex.DEERRORCODE_TAG.DE_SUCCESS) throw (new ApplicationException("digiEAR_index " + iError.ToString()));
                            //this.listBox1.Items.Add("index medFile: " + medFile);
                            backgroundWorker1.ReportProgress(hIndex);

                            int iSecond = 0;
                            while (true)
                            {
                                Thread.Sleep(3000);
                                iError = index_obj.digiEAR_indexGetStatus(hIndex, out iSecond);
                                if (iError != DigiEarIndex.DEERRORCODE_TAG.DE_SUCCESS)
                                {
                                    backgroundWorker1.ReportProgress(-1);
                                    break;
                                }
                                else backgroundWorker1.ReportProgress(iSecond);
                            }
                        }
                    }
                }

                // close the handle
                //
                iError = index_obj.digiEAR_indexCloseHandle(hIndex);
                //this.listBox1.Items.Add(iError.ToString());
                backgroundWorker1.ReportProgress(hIndex);
            }
            catch (Exception e1)
            {
                //this.listBox1.Items.Add(e1.Message);
                if (0 < hIndex)
                {
                    index_obj.digiEAR_indexCancel(hIndex);
                    index_obj.digiEAR_indexCloseHandle(hIndex);
                }
            }

        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            listBox1.Items.Add("index status: " + e.ProgressPercentage.ToString());
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if ((e.Cancelled == true))
            {
                listBox1.Items.Add("index Cancel !");
            }
            else if (!(e.Error == null))
            {
                listBox1.Items.Add("index Error: " + e.Error.Message);
            }
            else
            {
                listBox1.Items.Add("index done !!");
                search();
                listBox1.Items.Add("search done !!");
            }
        }
    }
}
