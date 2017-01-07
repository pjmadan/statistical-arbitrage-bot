using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DevExpress.XtraCharts;
using Libraries.Common.Data;
using System.Data.SqlClient;
using System.Threading;

namespace StatisticalArbitrageBot
{
    public partial class GraphicalDisplay : Form
    {
        public SqlConnection thisConnection;
        List<assets> assetstoanalyze;
        string begin_date;
        string end_date;
        public bool refresh = true;
        public delegate void refreshing();
        public refreshing mydelg;

        public GraphicalDisplay()
        {
            InitializeComponent();
        }

        private void GraphicalDisplay_Load(object sender, EventArgs e)
        {
            mydelg = new refreshing(creategraphs);
            assetstoanalyze=getassets();
            if (assetstoanalyze != null)
            {
                foreach (assets item in assetstoanalyze)
                {
                    checkedComboBoxEdit1.Properties.Items.Add(item.asset);
                }
            }

            var t2 = new Thread(() => refreshgraph());
            t2.Start();
        }

        private void refreshgraph()
        {
            while (refresh)
            {
                if (begin_date != null && end_date != null && checkedComboBoxEdit1.EditValue != null)
                {

                  this.BeginInvoke(mydelg);
                  System.Threading.Thread.Sleep(100000);
                }

            }

        }

        public List<assets> getassets()
        {
            string sql = "getassets";
            List<assets> assets = null;

            try
            {
                if (thisConnection != null)
                {
                     SqlCommand cmd = new SqlCommand(sql, thisConnection) { CommandType = CommandType.StoredProcedure, CommandText = sql };
                     assets = SQLHelpers.ExecuteDataFetch<assets>(cmd);

                    return assets;
                }

                else
                {
                    return null;
                }
            }

            catch (Exception ex)
            {
                throw ex;
            }
        }


        public List<graphassets> getseries(string ids, string begindate, string enddate)
        {
            string sql = "getpriceactionforassets";
            List<graphassets> assets = null;

            try
            {
                if (thisConnection != null)
                {
                    SqlCommand cmd = new SqlCommand(sql, thisConnection) { CommandType = CommandType.StoredProcedure};
                    SqlParameter pcustomerName = new SqlParameter("@assetids", SqlDbType.VarChar,200) { Value = ids };
                    SqlParameter begindates = new SqlParameter("@begindate", SqlDbType.VarChar, 200) { Value = begindate };
                    SqlParameter enddates = new SqlParameter("@enddate", SqlDbType.VarChar, 200) { Value = enddate };
                    cmd.Parameters.Add(pcustomerName);
                    cmd.Parameters.Add(begindates);
                    cmd.Parameters.Add(enddates);
                    assets = SQLHelpers.ExecuteDataFetch<graphassets>(cmd);

                    return assets;
                }

                else
                {
                    return null;
                }
            }

            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void checkedComboBoxEdit1_EditValueChanged(object sender, EventArgs e)
        {
            if (begin_date != null && end_date != null)
            {
               creategraphs();
            }

        }

        private string calculateassetids(string assets)
        {
            foreach (assets item in assetstoanalyze)
            {
                if (assets.ToLower().Trim() == item.asset.ToString().ToLower().Trim())
                {
                    return item.assetid;
                }
            }

            return "";
        }


        private  void creategraphs()
        {

                object dblock = new object();
                lock (dblock)
                {
                DevExpress.XtraCharts.ChartControl control = new ChartControl();
                control = chartControl1;
                control.DataSource = null;
                control.Series.Clear();
                control.Titles.Clear();
                string assets = checkedComboBoxEdit1.EditValue.ToString();
                int i = 0;
                if (assets != "" && assets != null)
                {
                    string[] assetvalues = assets.Split(',');
                    string assetid = "";
                    foreach (string s in assetvalues)
                    {
                        assetid = assetid + ',' + calculateassetids(s);
                    }

                    assetid = assetid.Substring(1, assetid.Length - 1);


                    string[] assetids = assetid.Split(',');
                    Series[] newseries = new Series[assetids.Length];

                    foreach (string s in assetids)
                    {
                        List<graphassets> asset = new List<graphassets>();
                        asset = getseries(s, begin_date, end_date);
                        newseries[i] = new Series("Price action for " + s.ToString(), ViewType.Spline);
                        newseries[i].DataSource = asset;
                        newseries[i].ArgumentDataMember = "DateTime";
                        string[] valuemembers = new string[1];
                        valuemembers[0] = "Price";
                        newseries[i].ValueDataMembers.AddRange(valuemembers);
                        newseries[i].ArgumentScaleType = ScaleType.Qualitative;
                        newseries[i].ValueScaleType = ScaleType.Numerical;
                        newseries[i].Label.Visible = false;
                        i++;

                    }
                    control.Series.AddRange(newseries);
                    control.Legend.AlignmentHorizontal = LegendAlignmentHorizontal.Right;
                    control.Legend.AlignmentVertical = LegendAlignmentVertical.TopOutside;
                    control.Legend.Direction = LegendDirection.LeftToRight;
                    control.Titles.Add(new ChartTitle());
                    control.Titles[0].Text = "Asset Price Trend";
                    control.Titles[0].Dock = ChartTitleDockStyle.Top;
                    control.Legend.AlignmentHorizontal = LegendAlignmentHorizontal.Right;
                    control.Legend.AlignmentVertical = LegendAlignmentVertical.TopOutside;
                    control.Legend.Direction = LegendDirection.LeftToRight;
                    XYDiagram myDiagram = (XYDiagram)control.Diagram;
                    myDiagram.AxisX.Title.Text = "Date Time";
                    myDiagram.AxisY.Title.Text = "Asset Price";
                    myDiagram.AxisX.Title.Visible = true;
                    myDiagram.AxisY.Title.Visible = true;
                    myDiagram.AxisX.Label.Angle = -30;
                    myDiagram.AxisY.NumericOptions.Format = NumericFormat.Number;
                    myDiagram.AxisX.Label.Visible = false;

                }
            }
        }

        private void dateEdit1_EditValueChanged(object sender, EventArgs e)
        {
            begin_date = dateEdit1.EditValue.ToString();
            if (checkedComboBoxEdit1.EditValue != null && end_date != null)
            {
               creategraphs();
            }
        }

        private void dateEdit2_EditValueChanged(object sender, EventArgs e)
        {
            end_date = dateEdit2.EditValue.ToString();
            if (checkedComboBoxEdit1.EditValue != null && begin_date != null)
            {
              creategraphs();
            }
        }

        private void chartControl1_ObjectHotTracked(object sender, HotTrackEventArgs e)
        {
            SeriesPoint point = e.AdditionalObject as SeriesPoint;
            if (point != null)
            {
            graphassets objectpointer = (graphassets)point.Tag;
            DateTime obj1 = objectpointer.DateTime;
            string obj11 = obj1.ToString("MM/dd H:mm:ss");
            decimal bid=objectpointer.BidPrice;
            decimal ask=objectpointer.AskPrice;
            decimal last = objectpointer.Price;
            string obj12 = objectpointer.Asset;
            int vol = objectpointer.Volume;
            toolTipController1.ShowHint(obj12 + " "+"Bid:"+bid+" Ask:"+ask+" Last:"+last +" Time:"+obj11+" Volume:"+vol);
            }
        }

        private void GraphicalDisplay_FormClosing(object sender, FormClosingEventArgs e)
        {
            refresh = false;
        }
    }

    public class assets
    {
        public string assetid {get;set;}
        public string asset {get;set;}
    }


    public class graphassets
    {
        public int Volume { get; set; }
        public string Asset { get; set; }
        public DateTime  DateTime { get; set; }
        public decimal Price { get; set; }
        public decimal BidPrice { get; set; }
        public decimal AskPrice { get; set; }
    }
}
