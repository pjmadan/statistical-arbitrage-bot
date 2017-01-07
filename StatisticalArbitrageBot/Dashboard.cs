using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Data.SqlClient;
using Libraries.Common.Data;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Configuration;
namespace StatisticalArbitrageBot
{
    public  partial class Dashboard : Form
    {
        bool runloop=true;
        public delegate void paintgrid(DataTable dt);
        public paintgrid paint;
        public delegate void paintaccountgrid(DataTable dt);
        public paintaccountgrid paintaccount;
        public delegate void paintordergrid(DataTable dt);
        public paintordergrid paintorder;
        public string begin_date = "";
        public string end_date = "";
        public SqlConnection Connection;

        public Dashboard()
        {
            InitializeComponent();
        }

        private void Dashboard_Load(object sender, EventArgs e)
        {
            paintaccount = new paintaccountgrid(paintaccountgrids);
            paintorder = new paintordergrid(paintordergrids);
            paint = new paintgrid(paintpositiongrid);

            var t1 = new Thread(() => loadaccountgrid());
            t1.Start();
            var t2 = new Thread(() => loadpositionsgrid());
            t2.Start();
            var t3 = new Thread(() => loadordergrid());
            t3.Start();
        }

        private void paintpositiongrid(DataTable structure)
        {
            if (structure.Rows.Count > 0)
            {
                gridControl1.DataSource = null;
                gridControl1.DataSource = structure;
            }

        }

        private void paintaccountgrids(DataTable structure)
        {
            if (structure.Rows.Count > 0)
            {
                gridControl2.DataSource = null;
                gridControl2.DataSource = structure;
            }

        }

        private void paintordergrids(DataTable structure)
        {
            if (structure.Rows.Count > 0)
            {
                gridControl4.DataSource = null;
                gridControl4.DataSource = structure;
            }

        }

        private static DataTable ToDataTable(List<accountpositioninformation> mypositions)
        {
            DataTable table = new DataTable();
            table.Columns.Add("account_number");
            table.Columns.Add("assetid");
            table.Columns.Add("asset_type");
            table.Columns.Add("purchaseprice");
            table.Columns.Add("qty");
            table.Columns.Add("TotalCapitalAllocation");
            table.Columns.Add("asset");
            table.Columns.Add("gainloss");



            foreach (accountpositioninformation prop in mypositions)
            {
                DataRow row = table.NewRow();
                row["account_number"] = prop.account_number;
                row["assetid"] = prop.assetid;
                row["asset_type"] = prop.asset_type;
                row["purchaseprice"] = prop.purchaseprice;
                row["qty"] = prop.qty;
                row["TotalCapitalAllocation"] = prop.TotalCapitalAllocation;
                row["asset"] = prop.asset;
                row["gainloss"] = prop.gainloss;
                table.Rows.Add(row);
            }
            return table;
        }

        private static DataTable ToOrderDataTable(List<orderinformation> myorders)
        {
            DataTable table = new DataTable();
            table.Columns.Add("OrderNumber");
            table.Columns.Add("AssetInfo");
            table.Columns.Add("OrderCost");
            table.Columns.Add("OrderType");
            table.Columns.Add("Quantity");
            table.Columns.Add("OrderStatus");
            table.Columns.Add("OrderPlacedTime");
            table.Columns.Add("OrderExecutedTime");



            foreach (orderinformation prop in myorders)
            {
                DataRow row = table.NewRow();
                row["OrderNumber"] = prop.OrderNumber;
                row["AssetInfo"] = prop.AssetInfo;
                row["OrderCost"] = prop.OrderCost;
                row["OrderType"] = prop.OrderType;
                row["Quantity"] = prop.Quantity;
                row["OrderStatus"] = prop.OrderStatus;
                row["OrderPlacedTime"] = prop.OrderPlacedTime;
                row["OrderExecutedTime"] = prop.OrderExecutedTime;
                table.Rows.Add(row);
            }
            return table;
        }

        private void loadpositionsgrid()
        {
            while (runloop)
            {
                System.Threading.Thread.Sleep(20000);
                List<accountpositioninformation> thislist = new List<accountpositioninformation>();
     
                if (MainEntry.PositionsInfo.Count > 0)
                {
                    thislist.AddRange(MainEntry.PositionsInfo);
                    DataTable dt = ToDataTable(thislist);
                    if (runloop)
                    {
                        this.BeginInvoke(paint, dt);
                    }
                }
            }
        }

        private void loadordergrid()
        {
            while (runloop)
            {
                System.Threading.Thread.Sleep(15000);
                List<orderinformation> thislist = new List<orderinformation>();
                if (MainEntry.OrdersInfo != null)
                {
                    if (MainEntry.OrdersInfo.Count > 0)
                    {
                        thislist.AddRange(MainEntry.OrdersInfo);
                        DataTable dt = ToOrderDataTable(thislist);
                        if (runloop)
                        {
                            this.BeginInvoke(paintorder, dt);
                        }
                    }
                }
            }
        } 

        private void loadaccountgrid()
        {
            while (runloop)
            {
                System.Threading.Thread.Sleep(30000);
                 DataTable dt = new DataTable();
                dt.Columns.Add("AccountNumber");
                dt.Columns.Add("MarginBalance");
                dt.Columns.Add("PurchasingPower");
                dt.Columns.Add("CashBalance");
                DataRow row = dt.NewRow();
                row["AccountNumber"] = MainEntry.account_balance.account_number;
                row["MarginBalance"] = MainEntry.account_balance.margin_purchasing_power;
                row["PurchasingPower"] = MainEntry.account_balance.available_margincash;
                row["CashBalance"] = MainEntry.account_balance.latest_available_balance;
                dt.Rows.Add(row);

                if (runloop)
                {
                    this.BeginInvoke(paintaccount, dt);
                }
                
            }
        }

        private void dateEdit1_EditValueChanged(object sender, EventArgs e)
        {
            begin_date = dateEdit1.EditValue.ToString();
        }

        private void dateEdit2_EditValueChanged(object sender, EventArgs e)
        {
            end_date = dateEdit2.EditValue.ToString();
        }

        private void simpleButton3_Click(object sender, EventArgs e)
        {
            string sql = "getlog";
            DataTable assets = new DataTable();
            gridControl3.DataSource = null;
            try
            {
                if (Connection != null)
                {
                    SqlCommand cmd = new SqlCommand(sql, Connection) { CommandType = CommandType.StoredProcedure };
                    cmd.Parameters.Add(new SqlParameter("@begin_date",begin_date));
                    cmd.Parameters.Add(new SqlParameter("@end_date",end_date));
                    assets = SQLHelpers.ExecuteDataFetch(cmd).Tables[0];
                    gridControl3.DataSource = assets;
                }

            }

            catch (Exception ex)
            {
                writelog("Dashboard Log: " + ex.Message);
            }
        }   
                      
        public  void writelog(string responsestring)
        {
            Object tolock = new Object();
            lock (tolock)
            {
                String insertCmd = "insert into log values (3,NULL," + responsestring + ",getdate())";
                SqlCommand myCommand = new SqlCommand(insertCmd, Connection);
                myCommand.ExecuteNonQuery();
            }

        }

        private void Dashboard_FormClosing(object sender, FormClosingEventArgs e)
        {
            runloop = false;
        }

        private void simpleButton2_Click(object sender, EventArgs e)
        {
            simpleButton2.Enabled = false;
            double order_id = 0;
            string orderlist = MainEntry.getorderlist(ConfigurationManager.AppSettings["AccountNumber"]);
            XmlDocument doc = new XmlDocument();

            if (orderlist != "")
            {
                doc.LoadXml(orderlist);
                XmlNodeList nodes = doc.GetElementsByTagName("order");

                if (nodes.Count > 0)
                {
                    foreach (XmlNode node in nodes)
                    {

                        XmlNodeList ind_nodelist = node.ChildNodes;
                        foreach (XmlNode ind_node in ind_nodelist)
                        {
                            if (ind_node.Name.ToLower() == "orderid")
                            {
                                order_id = Convert.ToDouble(ind_node.InnerText);
                                MainEntry.cancelassetorder(order_id.ToString());
                            }
                        }
                    }
                }
            }

            simpleButton2.Enabled = true;
        }
    }
}
