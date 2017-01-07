using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Data.SqlClient;
using Libraries.Common.Data;
using System.Threading;
namespace StatisticalArbitrageBot
{
    public partial class AssetMonitor : Form
    {
        public SqlConnection thiconnect;
        List<assets> assetstoanalyze;
        private string begin_date;
        private string end_date;
        private string assetids="";

        public AssetMonitor()
        {
            InitializeComponent();
        }
        public List<assets> getassets()
        {
            string sql = "getassets";
            List<assets> assets = null;

            try
            {
                if (thiconnect != null)
                {
                    SqlCommand cmd = new SqlCommand(sql, thiconnect) { CommandType = CommandType.StoredProcedure, CommandText = sql };
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

        private void loadgrid()
        {

                string sql = "getassetmonitor";
                List<assetmonitor> assets = null;
                gridControl1.DataSource = null;
                try
                {
                    if (thiconnect != null)
                    {
                        SqlCommand cmd = new SqlCommand(sql, thiconnect) { CommandType = CommandType.StoredProcedure };
                        SqlParameter pcustomerName = new SqlParameter("@assetids", SqlDbType.VarChar, 200) { Value = assetids };
                        SqlParameter begindates = new SqlParameter("@begin_date", SqlDbType.DateTime, 200) { Value = begin_date };
                        SqlParameter enddates = new SqlParameter("@end_date", SqlDbType.DateTime, 200) { Value = end_date };
                        cmd.Parameters.Add(pcustomerName);
                        cmd.Parameters.Add(begindates);
                        cmd.Parameters.Add(enddates);
                        assets = SQLHelpers.ExecuteDataFetch<assetmonitor>(cmd);
                        gridControl1.DataSource = assets;
                    }

                }

                catch (Exception ex)
                {
                    throw ex;
                }

            }

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            if (assetids != "" & begin_date != null & end_date != null)
            {
                loadgrid();
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

        private void AssetMonitor_Load(object sender, EventArgs e)
        {
            assetstoanalyze = getassets();
            if (assetstoanalyze != null)
            {
                foreach (assets item in assetstoanalyze)
                {
                    checkedComboBoxEdit1.Properties.Items.Add(item.asset);
                }
            }
        }

        private void dateEdit1_EditValueChanged(object sender, EventArgs e)
        {
            if (dateEdit1.EditValue != null)
            {
                begin_date = dateEdit1.EditValue.ToString();
            }
        }

        private void checkedComboBoxEdit1_EditValueChanged(object sender, EventArgs e)
        {
               string assets1 = checkedComboBoxEdit1.EditValue.ToString();
                if (assets1 != "" && assets1 != null)
                {
                    string[] assetvalues = assets1.Split(',');
                    foreach (string s in assetvalues)
                    {
                        assetids = assetids + ',' + calculateassetids(s);
                    }

                    assetids = assetids.Substring(1, assetids.Length - 1);
                }
        }

        private void dateEdit2_EditValueChanged(object sender, EventArgs e)
        {
            if (dateEdit2.EditValue != null)
            {
                end_date = dateEdit2.EditValue.ToString();
            }
        }


    }


    public class assetmonitor
    {
        public string Asset {get;set;}
        public decimal BidPrice { get; set; }
        public int BidSize { get; set; }
        public decimal AskPrice { get; set; }
        public int AskSize { get; set; }
        public decimal LastPrice { get; set; }
        public int Volume { get; set; }
        public int AssetID { get; set; }
        public DateTime MonitorDate { get; set; }
        public int IsTraded { get; set; }
        public int IsMonitored { get; set; }
    }

}
