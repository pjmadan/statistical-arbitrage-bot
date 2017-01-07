using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Configuration;
using System.Collections;
using System.Threading;
using System.Data.SqlClient;
using System.Web;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Libraries.Common.Data;
using System.Text.RegularExpressions;
namespace StatisticalArbitrageBot
{
    public partial class MainEntry : Form
    {
        public static volatile List<asset_information> AssetInfo = null;
        public static volatile List<accountpositioninformation> PositionsInfo = null;
        public static volatile List<orderinformation> OrdersInfo = null;
        public static volatile bool stop_process = true;
        static volatile SqlConnection thisConnection = null;
        public static volatile accountbalanceinformation account_balance = null;
        public volatile accountpositioninformation account_position = null;
        public static volatile string lastworkedonasset = "";
        public static volatile string lastaccesstokentime = "";
        public static volatile string optioncommission = "";
        public static volatile string maxpricedeviation = "";
        public static volatile Dashboard form;
        Manager oauth = new Manager();

        public MainEntry()
        {
            InitializeComponent();
        }

        private void simpleButton1_Click(object sender, EventArgs e)
        {
            simpleButton4.Enabled = true;
            simpleButton5.Enabled = true;
            simpleButton2.Enabled = true;
            panel3.SendToBack();
            panel3.Visible = false;
            panel1.BringToFront();
            panel2.BringToFront();
            simpleButton1.Enabled = false;

            Dashboard s = new Dashboard();
            s.Connection = thisConnection;
            CheckMdiChildren(s);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            KillProcess("iexplore");
            AssetInfo = new List<asset_information>();
            PositionsInfo = new List<accountpositioninformation>();
            OrdersInfo = new List<orderinformation>();
            labelControl4.Text = "Initializing...";
            labelControl4.Refresh();
            labelControl4.Text = "Getting today's assets...";
            account_position = new accountpositioninformation();
            string server_name = ConfigurationManager.AppSettings["Server"];
            string db_name = ConfigurationManager.AppSettings["Database"];
            optioncommission = ConfigurationManager.AppSettings["AssetCommission"];
            maxpricedeviation = ConfigurationManager.AppSettings["MaximumPriceDeviation"];
            account_balance = new accountbalanceinformation();
            account_balance.account_number = ConfigurationManager.AppSettings["AccountNumber"];
            labelControl4.Text = "Server:" + server_name;
            labelControl4.Text = "Database:" + db_name;
            labelControl4.Text = "Establishing DB connection...";
            labelControl4.Refresh();
            thisConnection = new SqlConnection("Server=" + server_name + ";Database=" + db_name + ";Trusted_Connection=True;MultipleActiveResultSets=True");
            thisConnection.Open();
            labelControl4.Text = "Connection Established...";
            SqlCommand getassetinfo = new SqlCommand("select assetid,case assettype when 1 then 'put' else 'call' end ,ticker,strike,expiry_month,expiry_year,expiry_day,totrade from dbo.Assets where tomonitor=1 order by ticker, strike asc", thisConnection);
            SqlDataAdapter assetadapter = new SqlDataAdapter(getassetinfo);
            DataSet assetinfo = new DataSet();
            assetadapter.Fill(assetinfo);


            for (int i = 0; i <= assetinfo.Tables[0].Rows.Count - 1; i++)
            {

                asset_information stocks = new asset_information();
                stocks.id = i;
                stocks.stock_name = assetinfo.Tables[0].Rows[i].ItemArray[2].ToString();
                stocks.stock_lastprice = "00.00";
                stocks.stock_asksize = "0";
                stocks.stock_bidsize = "0";
                stocks.stock_lastbid = "0";
                stocks.stock_lastask = "0";
                stocks.stock_volume = "0";
                stocks.assetid = assetinfo.Tables[0].Rows[i].ItemArray[0].ToString();
                stocks.option_expirymonth = assetinfo.Tables[0].Rows[i].ItemArray[4].ToString();
                stocks.option_expiryday = assetinfo.Tables[0].Rows[i].ItemArray[6].ToString();
                stocks.option_expiryyear = assetinfo.Tables[0].Rows[i].ItemArray[5].ToString();
                stocks.option_type = assetinfo.Tables[0].Rows[i].ItemArray[1].ToString();
                stocks.stock_strikeprice = assetinfo.Tables[0].Rows[i].ItemArray[3].ToString();
                stocks.to_trade = assetinfo.Tables[0].Rows[i].ItemArray[7].ToString();
                AssetInfo.Add(stocks);

            }

        }

        public  void checkendtime()
        {
            string[] endtime = ConfigurationManager.AppSettings["EndTime"].Split(':');
            string endtimehour = endtime[0];
            string endtimemin = endtime[1];
            double configuredendtime = Convert.ToDouble(endtimehour) * 60 * 60 + Convert.ToDouble(endtimemin) * 60;
            double order_id = 0;
            while (stop_process)
            {
                try
                {
                    string hour = System.DateTime.Now.Hour.ToString();
                    string min = System.DateTime.Now.Minute.ToString();
                    double nowtime = Convert.ToDouble(hour) * 60 * 60 + Convert.ToDouble(min) * 60;

                    if (Convert.ToDouble(nowtime) >= Convert.ToDouble(configuredendtime))
                    {

                        stop_process = false;
                        string orderlist = getorderlist(ConfigurationManager.AppSettings["AccountNumber"]);
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
                                            cancelassetorder(order_id.ToString());
                                        }
                                    }
                                }
                            }
                        }

                    }
                    System.Threading.Thread.Sleep(60000);
                }

                catch (Exception exp)
                {
                  string  exp1=exp.Message.Replace("'","''");
                  writelog("'Check EndTime: " + exp1+"'");
                    checkendtime();
                }

                System.Threading.Thread.Sleep(10000);
            }

        }

        public  void KillProcess(string processname)
        {
            System.Diagnostics.Process[] procs = System.Diagnostics.Process.GetProcessesByName(processname);

            foreach (System.Diagnostics.Process proc in procs)
            {
                proc.Kill(); // Close it down.
            }
        }

        public  void runlooping(SqlConnection mysql)
        {
            string configured_starttimehour = ConfigurationManager.AppSettings["StartTime"];
            string[] parsed_configstarttime = configured_starttimehour.Split(':');
            string configured_endtimehour = ConfigurationManager.AppSettings["EndTime"];
            string[] parsed_configendtime = configured_endtimehour.Split(':');
            while (stop_process)
            {
                DateTime start_time = System.DateTime.Now;
                string parsed_time = start_time.ToString("H:mm");
                string[] parsed_time1 = parsed_time.Split(':');

                if (Convert.ToDouble(parsed_time1[0].Trim()) == Convert.ToDouble(parsed_configstarttime[0].Trim()) && Convert.ToDouble(parsed_time1[0].Trim()) <= Convert.ToDouble(parsed_configendtime[0].Trim()))
                {
                    if (Convert.ToDouble(parsed_time1[1].Trim()) >= Convert.ToDouble(parsed_configstarttime[1].Trim()))
                    {
                        var t4 = new Thread(() => getaccountbalance(thisConnection));
                        t4.Start();
                        var t = new Thread(() => assetquotemonitoring(thisConnection, ""));
                        t.Start();
                        var t2 = new Thread(() => sellassets(thisConnection));
                        t2.Start();
                        var t11 = new Thread(() => buygenerator(mysql));
                        t11.Start();
                        var t3 = new Thread(() => getaccountpositions(thisConnection));
                        t3.Start();
                        var t5 = new Thread(() => callorders());
                        t5.Start();
                        break;
                    }
                }


                if (Convert.ToDouble(parsed_time1[0].Trim()) > Convert.ToDouble(parsed_configstarttime[0].Trim()) && Convert.ToDouble(parsed_time1[0].Trim()) < Convert.ToDouble(parsed_configendtime[0].Trim()))
                {
                    var t4 = new Thread(() => getaccountbalance(thisConnection));
                    t4.Start();
                    var t3 = new Thread(() => getaccountpositions(thisConnection));
                    t3.Start();
                    var t = new Thread(() => assetquotemonitoring(thisConnection, ""));
                    t.Start();
                    var t2 = new Thread(() => sellassets(thisConnection));
                    t2.Start();
                    var t11 = new Thread(() => buygenerator(mysql));
                    t11.Start();
                    var t5 = new Thread(() => callorders());
                    t5.Start();
                    break;
                }

                System.Threading.Thread.Sleep(700);
            }

        }

        public  void getaccountlist(SqlConnection continueconnection)
        {
            System.Threading.Thread.Sleep(1100);
            string access_token = "";
            string access_token_secret = "";
            String insertCmd = "Select top 1 tokenid, tokensecret from dbo.AccessToken order by creationdatetime desc";
            SqlCommand myCommand = new SqlCommand(insertCmd, continueconnection);
            SqlDataAdapter adapter = new SqlDataAdapter(myCommand);
            DataSet tokens = new DataSet();
            adapter.Fill(tokens);
            using (tokens)
            {
                foreach (DataRow row in tokens.Tables[0].Rows)
                {
                    access_token = row[0].ToString();
                    access_token_secret = row[1].ToString();
                }
            }
            adapter.Dispose();

            string oath_consumer = ConfigurationManager.AppSettings["oauth_consumer_key"];
            string oath_secret = ConfigurationManager.AppSettings["consumer_secret"];
            string uri = ConfigurationManager.AppSettings["AccessAccountList"];
            //Request to constantly poll the stocks and strike prices in question'
            Manager accountmanager = new Manager(oath_consumer, oath_secret, access_token, access_token_secret);
            var authzHeader = accountmanager.GetAuthorizationHeader(uri, "GET");

            //prepare the token request
            var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);
            request.Headers.Add("Authorization", authzHeader);
            request.Method = "GET";
            try
            {
                using (var response = (System.Net.HttpWebResponse)request.GetResponse())
                {
                    Stream reader = response.GetResponseStream();
                    StreamReader accountlist = new StreamReader(reader);
                    string accountlist_response = accountlist.ReadToEnd();
                }
            }

            catch (Exception ex)
            {
                string exp1 = ex.Message.Replace("'", "''");
                writelog("'Account List: " + exp1 + "'");
                getaccountlist(thisConnection);
            }

        }

        public  void getaccountbalance(SqlConnection continueconnection)
        {
            while (stop_process)
            {
                string access_token = "";
                string access_token_secret = "";

                String insertCmd = "Select top 1 tokenid, tokensecret from dbo.AccessToken order by creationdatetime desc";
                SqlCommand myCommand = new SqlCommand(insertCmd, continueconnection);
                SqlDataAdapter adapter = new SqlDataAdapter(myCommand);
                DataSet tokens = new DataSet();
                adapter.Fill(tokens);
                using (tokens)
                {
                    foreach (DataRow row in tokens.Tables[0].Rows)
                    {
                        access_token = row[0].ToString();
                        access_token_secret = row[1].ToString();
                    }
                }
                adapter.Dispose();

                string oath_consumer = ConfigurationManager.AppSettings["oauth_consumer_key"];
                string oath_secret = ConfigurationManager.AppSettings["consumer_secret"];
                string uri = ConfigurationManager.AppSettings["AccessAccountBalance"];
                string account_number = ConfigurationManager.AppSettings["AccountNumber"];
                uri = uri + account_number;
                //Request to constantly poll the stocks and strike prices in question'
                Manager accountmanager = new Manager(oath_consumer, oath_secret, access_token, access_token_secret);
                var authzHeader = accountmanager.GetAuthorizationHeader(uri, "GET");

                //prepare the token request
                var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);
                request.Headers.Add("Authorization", authzHeader);
                request.Method = "GET";
                try
                {
                    using (var response = (System.Net.HttpWebResponse)request.GetResponse())
                    {
                        Stream reader = response.GetResponseStream();
                        StreamReader accountbalance = new StreamReader(reader);
                        string accountbalance_response = accountbalance.ReadToEnd();
                        string account_balance1 = parseXMLResponse(accountbalance_response, "marginBalance");
                        string cash_balance = parseXMLResponse(accountbalance_response, "cashAvailableForWithdrawal");
                        string purchasing_power = parseXMLResponse(accountbalance_response, "marginBalanceWithdrawal");
                        if (account_balance.latest_available_balance != account_balance1)
                        {
                            account_balance.latest_available_balance = cash_balance;
                            account_balance.margin_purchasing_power = account_balance1;
                            account_balance.account_number = account_number;
                            account_balance.available_margincash = purchasing_power;
                        }
                    }
                }

                catch (Exception ex)
                {
                    string exp1 = ex.Message.Replace("'", "''");
                    writelog("'Account Balance: " + exp1 + "'");
                    getaccountbalance(thisConnection);
                }
                finally
                {
                    System.Threading.Thread.Sleep(1200);
                }
            }

        }

        public  void getaccountpositions(SqlConnection continueconnection)
        {
            string account_number = "";
            while (stop_process)
            {
                ArrayList Assets = new ArrayList();
                System.Threading.Thread.Sleep(1000);
                Assets.AddRange(AssetInfo);
                string access_token = "";
                string access_token_secret = "";
                string accountid = "";

                if (PositionsInfo.Count >= 1)
                {
                    PositionsInfo.RemoveRange(0, PositionsInfo.Count);
                }

                String insertCmd = "Select top 1 tokenid, tokensecret from dbo.AccessToken order by creationdatetime desc";
                SqlCommand myCommand = new SqlCommand(insertCmd, continueconnection);
                SqlDataAdapter adapter = new SqlDataAdapter(myCommand);
                DataSet tokens = new DataSet();
                adapter.Fill(tokens);
                using (tokens)
                {
                    foreach (DataRow row in tokens.Tables[0].Rows)
                    {
                        access_token = row[0].ToString();
                        access_token_secret = row[1].ToString();
                    }
                }
                adapter.Dispose();
                string oath_consumer = ConfigurationManager.AppSettings["oauth_consumer_key"];
                string oath_secret = ConfigurationManager.AppSettings["consumer_secret"];
                string uri = ConfigurationManager.AppSettings["AccessAccountPositions"];
                account_number = ConfigurationManager.AppSettings["AccountNumber"];
                //Request to constantly poll the stocks and strike prices in question'
                Manager accountmanager = new Manager(oath_consumer, oath_secret, access_token, access_token_secret);
                uri = uri + account_number;
                var authzHeader = accountmanager.GetAuthorizationHeader(uri, "GET");

                //prepare the token request
                var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);
                request.Headers.Add("Authorization", authzHeader);
                request.Method = "GET";

                try
                {
                    using (var response = (System.Net.HttpWebResponse)request.GetResponse())
                    {
                        Stream reader = response.GetResponseStream();
                        StreamReader accountlist = new StreamReader(reader);
                        string accountpositions_response = accountlist.ReadToEnd();
                        XmlDocument positionsxml = new XmlDocument();
                        positionsxml.LoadXml(accountpositions_response);
                        XmlNodeList nodes = positionsxml.GetElementsByTagName("AccountPositions");
                        if (nodes!=null)
                        {
                        foreach (XmlNode node in nodes)
                        {
                            if (node.Name.ToLower() == "accountpositions")
                            {

                                XmlNodeList positions = node.ChildNodes;
                                foreach (XmlNode children in positions)
                                {
                                    string purchase_price = "";
                                    string symbol = "";
                                    string quantity = "";
                                    string costbasis = "";
                                    string strike = "";
                                    string exp_yr = "";
                                    string exp_month = "";
                                    string exp_day = "";
                                    string gainloss = "";
                                    string assettype = "";
                                    string assetid = "";
                                    string type = "";

                                    if (children.Name.ToLower() == "accountposition")
                                    {
                                        XmlNodeList positions1 = children.ChildNodes;
                                        foreach (XmlNode children1 in positions1)
                                        {
                                            if (children1.Name.ToLower() == "costbasis")
                                            {
                                                costbasis = children1.InnerText;
                                            }

                                            if (children1.Name.ToLower() == "accountid")
                                            {
                                                accountid = children1.InnerText;
                                            }

                                            if (children1.Name.ToLower() == "qty")
                                            {
                                                quantity = children1.InnerText;
                                            }

                                            if (children1.Name.ToLower() == "productid")
                                            {
                                                XmlNodeList newpositions = children1.ChildNodes;
                                                foreach (XmlNode node1 in newpositions)
                                                {
                                                    if (node1.Name.ToLower() == "symbol")
                                                    {
                                                        symbol = node1.InnerText;

                                                    }

                                                    if (node1.Name.ToLower() == "strikeprice")
                                                    {
                                                        strike = node1.InnerText;

                                                    }

                                                    if (node1.Name.ToLower() == "callput")
                                                    {
                                                        assettype = node1.InnerText;

                                                    }

                                                    if (node1.Name.ToLower() == "expyear")
                                                    {
                                                        exp_yr = node1.InnerText;

                                                    }

                                                    if (node1.Name.ToLower() == "expmonth")
                                                    {
                                                        exp_month = node1.InnerText;

                                                    }

                                                    if (node1.Name.ToLower() == "expday")
                                                    {
                                                        exp_day = node1.InnerText;

                                                    }

                                                }

                                            }

                                        }

                                        purchase_price = Math.Abs(Convert.ToDecimal(Math.Round((Convert.ToDouble(costbasis) / (Convert.ToDouble(quantity)*100)), 2).ToString())).ToString();

                                        foreach (asset_information asset in Assets)
                                        {
                                            string option_type = "";

                                            if (asset.option_type == "1")
                                            {
                                                option_type = "PUT";
                                            }

                                            if (asset.option_type.ToLower() == "2")
                                            {
                                                option_type = "CALL";
                                            }


                                            if (asset.option_type.ToLower() == "put")
                                            {
                                                option_type = "PUT";
                                            }

                                            if (asset.option_type.ToLower() == "call")
                                            {
                                                option_type = "CALL";
                                            }

                                            if (asset.stock_name.ToLower() == symbol.ToLower() && option_type.ToLower() == assettype.ToLower() && Math.Round(Convert.ToDouble(asset.stock_strikeprice), 2) == Math.Round(Convert.ToDouble(strike), 2) && Math.Round(Convert.ToDouble(asset.option_expiryyear), 2) == Math.Round(Convert.ToDouble(exp_yr), 2) && Math.Round(Convert.ToDouble(asset.option_expirymonth), 2) == Math.Round(Convert.ToDouble(exp_month), 2) && Math.Round(Convert.ToDouble(asset.option_expiryday), 2) == Math.Round(Convert.ToDouble(exp_day), 2))
                                            {
                                                assetid = asset.assetid;
                                                type = asset.option_type;
                                            }

                                        }

                                        gainloss = getgainloss(purchase_price, assetid);
                                        accountpositioninformation myposition = new accountpositioninformation();
                                        myposition.account_number = account_number;
                                        myposition.assetid = assetid;
                                        myposition.purchaseprice = purchase_price;
                                        myposition.gainloss = gainloss;
                                        myposition.qty = Convert.ToInt32(Math.Abs(Convert.ToDecimal(quantity)));
                                        myposition.asset_type = type;
                                        myposition.TotalCapitalAllocation = costbasis;
                                        myposition.asset = getassetfromid(assetid);
                                        PositionsInfo.Add(myposition);
                                    }

                                }


                            }



                        }
                    }

                    }

                    foreach (accountpositioninformation position in PositionsInfo)
                    {
                        if (position != null)
                        {
                            //Now check if order information already exists in AssetPurchaseTable

                            if (position.assetid!="")
                            {
                            SqlCommand cmd1 = new SqlCommand("Select assetid,asset_type,purchase_price,quantity from dbo.AssetPurchasePrice where assetid=" + Convert.ToInt32(position.assetid.ToString()), thisConnection);
                            SqlDataAdapter adapt1 = new SqlDataAdapter(cmd1);
                            DataSet set = new DataSet();
                            adapt1.Fill(set);
                            //Purchase Information doesnt exist
                            string opt_type = "";
                            if (set.Tables[0].Rows.Count == 0)
                            {

                                if (position.asset_type.ToLower() == "put")
                                {
                                    opt_type = "1";
                                }

                                if (position.asset_type.ToLower() == "call")
                                {
                                    opt_type = "2";
                                }

                                if (position.asset_type.ToLower() == "1")
                                {
                                    opt_type = "1";
                                }

                                if (position.asset_type.ToLower() == "2")
                                {
                                    opt_type = "2";
                                }

                                SqlCommand cmd2 = new SqlCommand("Insert into dbo.AssetPurchasePrice (assetid,asset_type,purchase_price,quantity,totalcapitalallocation,creation_date) values (" + position.assetid + ", " + opt_type + "," + position.purchaseprice + ", " + position.qty + "," + position.TotalCapitalAllocation + ",getdate())", thisConnection);
                            //    cmd2.ExecuteNonQuery();
                            }

                            else
                            {
                                if (Math.Round(Convert.ToDouble(set.Tables[0].Rows[0].ItemArray[2].ToString()), 2) != Math.Round(Convert.ToDouble(position.purchaseprice), 2) || Math.Round(Convert.ToDouble(set.Tables[0].Rows[0].ItemArray[3].ToString()), 2) != Math.Round(Convert.ToDouble(position.qty), 2))
                                {

                                    SqlCommand cmd2 = new SqlCommand("Update dbo.AssetPurchasePrice " +
                                       " set purchase_price = " + Math.Round(Convert.ToDouble(position.purchaseprice),2) +
                                    " , quantity = " + Convert.ToInt32(position.qty) +
                                    " , creation_date=getdate()  where assetid= " + Convert.ToInt32(position.assetid), thisConnection);
                                    cmd2.ExecuteNonQuery();
                                }

                            }

                          }

                        }

                    }

                    //We also need to purge stale records.

                    String insertCmd1 = "select distinct assetid from dbo.AssetPurchasePrice";
                    SqlCommand myCommand1 = new SqlCommand(insertCmd1, continueconnection);
                    SqlDataAdapter adapter1 = new SqlDataAdapter(myCommand1);
                    DataSet tokens1 = new DataSet();
                    bool foundmatch = false;
                    adapter1.Fill(tokens1);

                    if (tokens1.Tables[0].Rows.Count > 0)
                    {
                        for (int i = 0; i <= tokens1.Tables[0].Rows.Count - 1; i++)
                        {
                            foreach (accountpositioninformation item in PositionsInfo)
                            {
                                if (tokens1.Tables[0].Rows[i].ItemArray[0].ToString() == item.assetid)
                                {

                                    foundmatch = true;

                                }

                            }

                            if (!foundmatch)
                            {
                                string cmd = "Delete from AssetPurchasePrice where assetid=" + Convert.ToInt32(tokens1.Tables[0].Rows[i].ItemArray[0].ToString());
                                inserintoDB(cmd);
                            }
                            else
                            {
                                foundmatch = false;
                            }

                        }

                    }
                }

                catch (Exception ex)
                {
                    string exp1 = ex.Message.Replace("'", "''");
                    writelog("'Account Positions: " + exp1 + "'");
                    getaccountpositions(thisConnection);
                }
                finally
                {
                    System.Threading.Thread.Sleep(2000);
                }
            }

        }

        static  void inserintoDB(string command)
        {
            Object Dblock = new object();
            lock (Dblock)
            {
                String insertCmd = command;
                SqlCommand myCommand = new SqlCommand(insertCmd, thisConnection);
                myCommand.ExecuteNonQuery();
            }
        }

        public  void assetquotemonitoring(SqlConnection continueconnection, string lastworked)
        {
            try
            {
                while (stop_process)
                {
                    foreach (asset_information stock in AssetInfo)
                    {
                        string last_trade = "";
                        string bid_size = "";
                        string bid = "";
                        string ask_size = "";
                        string ask = "";
                        string volume = "";
                        string assetquote_response = "";


                        if (lastworkedonasset == "")
                        {
                            lastworkedonasset = "-1";
                        }
                        if (stock.id.ToString() == (Convert.ToInt32(lastworkedonasset) + 1).ToString())
                        {
                            string access_token = "";
                            string opt_type = "";
                            string access_token_secret = "";
                            if (lastworkedonasset == "")
                            {
                                lastworkedonasset = stock.id.ToString();
                            }
                            String insertCmd = "Select top 1 tokenid, tokensecret from dbo.AccessToken order by creationdatetime desc";
                            SqlCommand myCommand = new SqlCommand(insertCmd, continueconnection);
                            SqlDataAdapter adapter = new SqlDataAdapter(myCommand);
                            DataSet tokens = new DataSet();
                            adapter.Fill(tokens);
                            using (tokens)
                            {
                                foreach (DataRow row in tokens.Tables[0].Rows)
                                {
                                    access_token = row[0].ToString();
                                    access_token_secret = row[1].ToString();
                                }
                            }
                            adapter.Dispose();


                            string oath_consumer = ConfigurationManager.AppSettings["oauth_consumer_key"];
                            string oath_secret = ConfigurationManager.AppSettings["consumer_secret"];
                            string uri = ConfigurationManager.AppSettings["GetOptionQuote"];




                            if (stock != null)
                            {
                                // System.Threading.Thread.Sleep(500);
                                string assetid = "";
                                switch (stock.option_type.ToLower())
                                {

                                    case "2":
                                        opt_type = "call";
                                        break;

                                    case "1":
                                        opt_type = "put";
                                        break;
                                }

                                switch (stock.option_type.ToLower())
                                {

                                    case "call":
                                        opt_type = "call";
                                        break;

                                    case "put":
                                        opt_type = "put";
                                        break;
                                }
                                uri = uri + stock.stock_name.ToLower() + ":2012:" + stock.option_expirymonth + ":" + stock.option_expiryday + ":" + opt_type + ":" + Math.Round(Convert.ToDouble(stock.stock_strikeprice), 2).ToString() + "?detailFlag=ALL";
                                //Request to constantly poll the stocks and strike prices in question'
                                Manager accountmanager = new Manager(oath_consumer, oath_secret, access_token, access_token_secret);
                                var authzHeader = accountmanager.GetAuthorizationHeader(uri, "GET");

                                //prepare the token request
                                var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);
                                request.Headers.Add("Authorization", authzHeader);
                                request.Method = "GET";
                                System.Threading.Thread.Sleep(6000);
                                try
                                {
                                    using (var response = (System.Net.HttpWebResponse)request.GetResponse())
                                    {
                                        Stream reader = response.GetResponseStream();
                                        StreamReader assetquote = new StreamReader(reader);
                                        assetquote_response = assetquote.ReadToEnd();

                                        last_trade = parseXMLResponse(assetquote_response, "lastTrade");
                                        bid_size = parseXMLResponse(assetquote_response, "bidSize");
                                        bid = parseXMLResponse(assetquote_response, "bid");
                                        ask_size = parseXMLResponse(assetquote_response, "askSize");
                                        ask = parseXMLResponse(assetquote_response, "ask");
                                        volume = parseXMLResponse(assetquote_response, "totalVolume");

                                        if (last_trade == "")
                                        {
                                            last_trade = "0";
                                        }

                                        if (bid_size == "")
                                        {
                                            bid_size = "0";
                                        }

                                        if (bid == "")
                                        {
                                            bid = "0";
                                        }

                                        if (ask_size == "")
                                        {
                                            ask_size = "0";
                                        }

                                        if (ask == "")
                                        {
                                            ask = "0";
                                        }

                                        if (volume == "")
                                        {
                                            volume = "0";
                                        }

                                        switch (stock.option_type.ToLower())
                                        {
                                            case "call":
                                                opt_type = "2";
                                                break;

                                            case "put":
                                                opt_type = "1";
                                                break;

                                            case "2":
                                                opt_type = "2";
                                                break;

                                            case "1":
                                                opt_type = "1";
                                                break;
                                        }


                                        String insertCmd1 = "Select top 1 assetid from dbo.Assets where ticker='" + stock.stock_name + "' and strike='"
                                            + stock.stock_strikeprice + "' and expiry_year='2012'and expiry_month='" + stock.option_expirymonth + "' and expiry_day='" + stock.option_expiryday + "' and assettype=" + opt_type + "order by creationdate desc";
                                        SqlCommand myCommand1 = new SqlCommand(insertCmd1, continueconnection);
                                        SqlDataAdapter adapter1 = new SqlDataAdapter(myCommand1);
                                        DataSet tokens1 = new DataSet();
                                        adapter1.Fill(tokens1);
                                        using (tokens1)
                                        {
                                            foreach (DataRow row in tokens1.Tables[0].Rows)
                                            {
                                                assetid = row[0].ToString();
                                            }
                                        }
                                        adapter1.Dispose();


                                        if (Math.Round(Convert.ToDouble(last_trade), 2).ToString() != Math.Round(Convert.ToDouble(stock.stock_lastprice), 2).ToString() || Math.Round(Convert.ToDouble(volume), 0).ToString() != Math.Round(Convert.ToDouble(stock.stock_volume), 0).ToString())
                                        {
                                            if (stock.option_type.ToLower() == "call")
                                            {
                                                stock.option_type = "2";
                                            }
                                            if (stock.option_type.ToLower() == "put")
                                            {
                                                stock.option_type = "1";
                                            }
                                            string cmd = "insert into AssetPriceAction values (" + Convert.ToDouble(assetid) + "," + Convert.ToDouble(stock.option_type) + "," + Convert.ToDouble(last_trade) + ","
                                                           + Convert.ToDouble(volume) + "," + Convert.ToDouble(bid) + "," + Convert.ToDouble(bid_size) + "," + Convert.ToDouble(ask) + "," + Convert.ToDouble(ask_size) + ",getdate())";
                                            inserintoDB(cmd);
                                            stock.stock_lastprice = last_trade;
                                            stock.stock_lastask = ask;
                                            stock.stock_lastbid = bid;
                                            stock.stock_asksize = ask_size;
                                            stock.stock_bidsize = bid_size;
                                            stock.stock_volume = volume;
                                            lastworkedonasset = stock.id.ToString();
                                            if (Convert.ToInt32(lastworkedonasset) + 1 == AssetInfo.Count)
                                            {
                                                lastworkedonasset = "-1";
                                                break;
                                            }
                                        }
                                        lastworkedonasset = stock.id.ToString();
                                        if (Convert.ToInt32(lastworkedonasset) + 1 == AssetInfo.Count)
                                        {
                                            lastworkedonasset = "-1";
                                        }
                                        //  writelog("Monitoring Asset..", assetquote_response, ConsoleColor.DarkYellow, "response");
                                    }

                                }

                                catch (Exception ex)
                                {
                                    if (lastworkedonasset != "")
                                    {
                                        string exp1 = ex.Message.Replace("'", "''");
                                        writelog("'Asset Quote Monitoring: " + exp1 + "'");
                                        assetquotemonitoring(thisConnection, lastworkedonasset + 1);
                                    }
                                    else
                                    {
                                        assetquotemonitoring(thisConnection, "");
                                    }
                                }

                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (lastworkedonasset != "")
                {
                    string exp1 = ex.Message.Replace("'", "''");
                    writelog("'Asset Quote Monitoring: " + exp1 + "'");
                    assetquotemonitoring(thisConnection, lastworkedonasset + 1);
                }
                else
                {
                    string exp1 = ex.Message.Replace("'", "''");
                    writelog("'Asset Quote Monitoring: " + exp1 + "'");
                    assetquotemonitoring(thisConnection, "");
                }
            }


        }

        public  string parseXMLResponse(string xmldocument, string key)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmldocument);
            XmlNodeList prices = doc.GetElementsByTagName(key);
            if (prices[0] != null)
            {
                if (!String.IsNullOrEmpty(prices[0].InnerText))
                {
                    return prices[0].InnerText;
                }
                else
                {
                    return "";
                }
            }
            else
            {
                return "";
            }
        }

        public static void writelog(string responsestring)
        {
            Object tolock = new Object();
            lock (tolock)
            {
                String insertCmd = "insert into log values (3,NULL," + responsestring+",getdate())";
                SqlCommand myCommand = new SqlCommand(insertCmd, thisConnection);
                myCommand.ExecuteNonQuery();
            }

        }

        public static void buygenerator(SqlConnection continuedconn)
        {
            //Generate list of asset pairs that need to be bought
            //Pairs are elligeble if pairprice is less than available balance
            //Find optimum price allocation for assetpairs that give lowest difference in price allocation
            while (stop_process)
            {
                decimal balance_money = Convert.ToDecimal(account_balance.margin_purchasing_power);
                if (balance_money>0)
                {
                ArrayList Assets = new ArrayList();
                Assets.AddRange(AssetInfo);
                
                string query = "select distinct ap.assetid1,b.maxpriceallocation,ab.assetid,ab.totalcapitalallocation ,ap.assetid2,b1.maxpriceallocation,ab1.assetid " +
                               ",ab1.totalcapitalallocation from assetpair ap join dbo.AssetMaxCapitalAllocation b on ap.assetid1=b.assetid " +
                               "join dbo.AssetMaxCapitalAllocation b1 on ap.assetid2=b1.assetid left join (select c.assetid,a.totrade,a.ticker,a.strike,a.expiry_month, " +
                               "a.expiry_day,a.expiry_year,c.totalcapitalallocation from dbo.Assets a left join dbo.AssetPurchasePrice c on a.assetid=c.assetid)ab " +
                               "on ap.assetid1=ab.assetid and ab.totrade=1 left join (select c.assetid,a.totrade,a.ticker,a.strike,a.expiry_month,a.expiry_day,a.expiry_year " +
                               ",c.totalcapitalallocation from dbo.Assets a left join dbo.AssetPurchasePrice c on a.assetid=c.assetid)ab1 on ap.assetid2=ab1.assetid and ab1.totrade=1 order by ap.assetid1,ap.assetid2";

                SqlCommand sqlcmd = new SqlCommand(query, continuedconn);
                SqlDataAdapter adapter = new SqlDataAdapter(sqlcmd);
                DataSet tokens = new DataSet();
                adapter.Fill(tokens);

                if (tokens.Tables[0].Rows.Count > 0)
                {

                    for (int i = 0; i <= tokens.Tables[0].Rows.Count - 1; i++)
                    {
                        string asset1ticker = "";
                        string asset1assettype = "";
                        string asset1asset_expiry_day = "";
                        string asset1asset_expiry_month = "";
                        string asset1asset_expiry_year = "";
                        int asset1no_assets = 0;
                        double asset1ask_quote = 0;
                        string asset1strike = "";
                        string asset2ticker = "";
                        string asset2assettype = "";
                        string asset2asset_expiry_day = "";
                        string asset2asset_expiry_month = "";
                        string asset2asset_expiry_year = "";
                        int asset2no_assets = 0;
                        double asset2ask_quote = 0;
                        string asset2strike = "";
                        bool asset1order_eligibility = false;
                        bool asset2order_eligibility = false;
                        int no_assets = 0;
                        decimal adjustedasset1price = 0;
                        decimal adjustedasset2price = 0;
                        string nullasset2 = "";
                        string nullasset1 = "";
                        decimal assetid1maxalloc = 0;
                        decimal assetid2maxalloc = 0;
                        decimal assetid1totalalloc = 0;
                        decimal assetid2totalalloc = 0;
                        decimal totalcost=0;
                        decimal remainderasset1 = 0;
                        decimal remainderasset2 = 0;


                        string assetid1 = tokens.Tables[0].Rows[i].ItemArray[0].ToString();
                        string assetid2 = tokens.Tables[0].Rows[i].ItemArray[4].ToString();


                        if (tokens.Tables[0].Rows[i].ItemArray[2] != null)
                        {
                            nullasset1 = tokens.Tables[0].Rows[i].ItemArray[2].ToString();
                        }

                        if (tokens.Tables[0].Rows[i].ItemArray[6] != null)
                        {
                            nullasset2 = tokens.Tables[0].Rows[i].ItemArray[6].ToString();
                        }

                        assetid1maxalloc = Convert.ToDecimal(tokens.Tables[0].Rows[i].ItemArray[1].ToString());
                        assetid2maxalloc = Convert.ToDecimal(tokens.Tables[0].Rows[i].ItemArray[5].ToString());



                        if (tokens.Tables[0].Rows[i].ItemArray[3].ToString() != "")
                        {
                           assetid1totalalloc = Convert.ToDecimal(tokens.Tables[0].Rows[i].ItemArray[3].ToString());
                        }

                        if (tokens.Tables[0].Rows[i].ItemArray[7].ToString() != "")
                        {
                            assetid2totalalloc = Convert.ToDecimal(tokens.Tables[0].Rows[i].ItemArray[7].ToString());
                        }

                        if ((nullasset1 =="" && nullasset2 != "") || (nullasset1 != "" && nullasset2 ==""))
                        {
                            //asset pairs that had partial execution
                            if (nullasset1 =="")
                            {
                                foreach (asset_information item in Assets)
                                {
                                    if (item.assetid == assetid1)
                                    {
                                        adjustedasset1price = Convert.ToDecimal(item.stock_lastask) * 100;
                                        no_assets = (int)Math.Floor(assetid1maxalloc/adjustedasset1price);
                                        asset1asset_expiry_day = item.option_expiryday;
                                        asset1asset_expiry_month = item.option_expirymonth;
                                        asset1asset_expiry_year = item.option_expiryyear;
                                        asset1assettype = item.option_type;
                                        asset1no_assets = no_assets;
                                        asset1strike = item.stock_strikeprice;
                                        asset1ticker = item.stock_name;
                                        asset1ask_quote = Convert.ToDouble(item.stock_lastask);
                                        break;
                                    }

                                }

                                switch (asset1assettype.ToLower().Trim())
                                {
                                    case "put":
                                        asset1assettype = "1";
                                        break;
                                    case "call":
                                        asset1assettype = "2";
                                        break;
                                }

                                if (no_assets != 0)
                                {

                                    if (no_assets == 1)
                                    {
                                        totalcost = (adjustedasset2price * no_assets) + Convert.ToDecimal(4.74);
                                    }

                                    else
                                    {
                                        totalcost = (adjustedasset2price * no_assets) + Convert.ToDecimal(4.74) + ((no_assets - 1) * Convert.ToDecimal(optioncommission));
                                    }


                                    remainderasset1 = assetid1maxalloc - totalcost;

                                    if (balance_money >= assetid1maxalloc)
                                    {
                                        if (remainderasset1 < 0)
                                        {
                                            while (remainderasset1 < 0)
                                            {
                                                remainderasset1 = 0;
                                                no_assets = no_assets - 1;
                                                if (no_assets == 0)
                                                {
                                                    break;
                                                }

                                                if (no_assets == 1)
                                                {
                                                    remainderasset1 = assetid1maxalloc - Convert.ToDecimal(4.74) - adjustedasset1price;
                                                }
                                                else
                                                {
                                                    remainderasset1 = assetid1maxalloc - Convert.ToDecimal(4.74) - ((no_assets - 1) * Convert.ToDecimal(optioncommission)) - (no_assets * adjustedasset1price);
                                                }
                                            }
                                        }

                                        if (balance_money >= totalcost && no_assets > 0 && Math.Abs((remainderasset1*100/assetid1maxalloc)- (((assetid2maxalloc-assetid2totalalloc)*100)/assetid2maxalloc))<=Convert.ToDecimal(maxpricedeviation))
                                        {

                                            asset1order_eligibility = assetordereligibility(asset1ticker, asset1assettype, asset1asset_expiry_day, asset1asset_expiry_month, asset1asset_expiry_year, asset1no_assets, (double)Math.Round(Convert.ToDecimal(asset1ask_quote), 2), "BUY_OPEN", asset1strike);
                                            if (asset1order_eligibility)
                                            {
                                                placeassetorder(asset1ticker, asset1strike, asset1assettype, asset1no_assets, asset1asset_expiry_year, asset1asset_expiry_month, asset1asset_expiry_day, "BUY_OPEN", (double)Math.Round(Convert.ToDecimal(asset1ask_quote), 2));
                                                string maxalloc = (Convert.ToDecimal((asset1ask_quote * 100 * asset1no_assets)) + Convert.ToDecimal((asset1no_assets * Convert.ToDecimal(optioncommission)))).ToString();
                                                string cmd = "insert into BuyPrices values (" + Convert.ToDouble(assetid1) + "," + Convert.ToDouble(asset1ask_quote) + "," + Convert.ToDouble(asset1no_assets) + ","
                                               + Convert.ToDouble(maxalloc) + ",getdate())";

                                                inserintoDB(cmd);
                                                cmd = "insert into Log values (1," + Convert.ToDouble(assetid1) + "," + "'Purchase order for price $" + Convert.ToDouble(asset1ask_quote) + "',getdate())";
                                                inserintoDB(cmd);
                                            }
                                        }
                                    }

                                }
                            }

                            else
                            {
                                foreach (asset_information item in Assets)
                                {
                                    if (item.assetid == assetid2)
                                    {
                                        adjustedasset2price = Convert.ToDecimal(item.stock_lastask) * 100;
                                        if (adjustedasset2price != 0)
                                        {
                                            no_assets = (int)Math.Floor(assetid2maxalloc / adjustedasset2price);
                                        }
                                        asset2asset_expiry_day = item.option_expiryday;
                                        asset2asset_expiry_month = item.option_expirymonth;
                                        asset2asset_expiry_year = item.option_expiryyear;
                                        asset2assettype = item.option_type;
                                        asset2no_assets = no_assets;
                                        asset2strike = item.stock_strikeprice;
                                        asset2ticker = item.stock_name;
                                        asset2ask_quote = Convert.ToDouble(item.stock_lastask);
                                        break;
                                    }

                                }

                                switch (asset2assettype.ToLower().Trim())
                                {
                                    case "put":
                                        asset2assettype = "1";
                                        break;
                                    case "call":
                                        asset2assettype = "2";
                                        break;
                                }

                                if (no_assets != 0)
                                {

                                    if (no_assets == 1)
                                    {
                                        totalcost = (adjustedasset2price * no_assets) + Convert.ToDecimal(4.74);
                                    }

                                    else
                                    {
                                        totalcost = (adjustedasset2price * no_assets) + Convert.ToDecimal(4.74) + ((no_assets - 1) * Convert.ToDecimal(optioncommission));
                                    }
                                    remainderasset2 = assetid2maxalloc - totalcost;


                                    if (balance_money >= assetid2maxalloc)
                                    {

                                        if (remainderasset2 < 0)
                                        {
                                            while (remainderasset2 < 0)
                                            {
                                                remainderasset2 = 0;
                                                no_assets = no_assets - 1;
                                                if (no_assets == 0)
                                                {
                                                    break;
                                                }

                                                if (no_assets == 1)
                                                {
                                                    remainderasset2 = assetid2maxalloc - Convert.ToDecimal(4.74) - adjustedasset2price;
                                                }
                                                else
                                                {
                                                    remainderasset2 = assetid2maxalloc - Convert.ToDecimal(4.74) - ((no_assets - 1) * Convert.ToDecimal(optioncommission)) - (no_assets * adjustedasset2price);
                                                }
                                            }

                                            if (no_assets == 1)
                                            {
                                                totalcost = (adjustedasset2price * no_assets) + Convert.ToDecimal(4.74);
                                            }

                                            else
                                            {
                                                totalcost = (adjustedasset2price * no_assets) + Convert.ToDecimal(4.74) + ((no_assets - 1) * Convert.ToDecimal(optioncommission));
                                            }

                                        }

                                        if (balance_money >= totalcost && no_assets > 0 && Math.Abs((remainderasset2 * 100 / assetid2maxalloc) - (((assetid1maxalloc - assetid1totalalloc) * 100) / assetid1maxalloc)) <= Convert.ToDecimal(maxpricedeviation))
                                        {
                                            asset2order_eligibility = assetordereligibility(asset2ticker, asset2assettype, asset2asset_expiry_day, asset2asset_expiry_month, asset2asset_expiry_year, asset2no_assets, (double)Math.Round(Convert.ToDecimal(asset2ask_quote), 2), "BUY_OPEN", asset2strike);
                                            if (asset2order_eligibility)
                                            {
                                                placeassetorder(asset2ticker, asset2strike, asset2assettype, asset2no_assets, asset2asset_expiry_year, asset2asset_expiry_month, asset2asset_expiry_day, "BUY_OPEN", (double)Math.Round(Convert.ToDecimal(asset2ask_quote), 2));
                                                string maxalloc = (Convert.ToDecimal((asset2ask_quote * 100 * asset2no_assets)) + Convert.ToDecimal((asset2no_assets * Convert.ToDecimal(optioncommission)))).ToString();
                                                string cmd = "insert into BuyPrices values (" + Convert.ToDouble(assetid2) + "," + Convert.ToDouble(asset2ask_quote) + "," + Convert.ToDouble(asset2no_assets) + ","
                                               + Convert.ToDouble(maxalloc) + ",getdate())";

                                                inserintoDB(cmd);
                                                cmd = "insert into Log values (1," + Convert.ToDouble(assetid2) + "," + "'Purchase order for price $" + Convert.ToDouble(asset2ask_quote) + "',getdate())";
                                                inserintoDB(cmd);

                                            }
                                        }

                                    }
                                }
                            }

                        }

                        if ( nullasset1=="" && nullasset2 == "")
                        {
                            //Is available capital enough to continue
                            if (balance_money >= assetid1maxalloc + assetid2maxalloc)
                            {
                                //find optimum price 
                                string return_val = getoptimumprice(assetid1, assetid2, assetid1maxalloc, assetid2maxalloc, balance_money);
                                if (return_val != "")
                                {
                                    string[] assetprices = return_val.Split(';');
                                    string[] asset1price = assetprices[0].Split(',');
                                    string[] asset2price = assetprices[1].Split(',');
                                    foreach (asset_information item in Assets)
                                    {
                                        if (item.assetid == assetid1)
                                        {
                                            asset1asset_expiry_day = item.option_expiryday;
                                            asset1asset_expiry_month = item.option_expirymonth;
                                            asset1asset_expiry_year = item.option_expiryyear;
                                            asset1assettype = item.option_type;
                                            asset1no_assets = Convert.ToInt32(asset1price[0]);
                                            asset1strike = item.stock_strikeprice;
                                            asset1ticker = item.stock_name;
                                            asset1ask_quote = Convert.ToDouble(asset1price[1]);
                                        }

                                        if (item.assetid == assetid2)
                                        {
                                            asset2asset_expiry_day = item.option_expiryday;
                                            asset2asset_expiry_month = item.option_expirymonth;
                                            asset2asset_expiry_year = item.option_expiryyear;
                                            asset2assettype = item.option_type;
                                            asset2no_assets = Convert.ToInt32(asset2price[0]);
                                            asset2strike = item.stock_strikeprice;
                                            asset2ticker = item.stock_name;
                                            asset2ask_quote = Convert.ToDouble(asset2price[1]);
                                        }

                                    }



                                    asset1order_eligibility = assetordereligibility(asset1ticker, asset1assettype, asset1asset_expiry_day, asset1asset_expiry_month, asset1asset_expiry_year, asset1no_assets, (double)Math.Round(Convert.ToDecimal(asset1ask_quote), 2), "BUY_OPEN", asset1strike);
                                    asset2order_eligibility = assetordereligibility(asset2ticker, asset2assettype, asset2asset_expiry_day, asset2asset_expiry_month, asset2asset_expiry_year, asset2no_assets, (double)Math.Round(Convert.ToDecimal(asset2ask_quote), 2), "BUY_OPEN", asset2strike);
                                    switch (asset1assettype.ToLower().Trim())
                                    {
                                        case "put":
                                            asset1assettype = "1";
                                            break;
                                        case "call":
                                            asset1assettype = "2";
                                            break;
                                    }

                                    switch (asset2assettype.ToLower().Trim())
                                    {
                                        case "put":
                                            asset2assettype = "1";
                                            break;
                                        case "call":
                                            asset2assettype = "2";
                                            break;
                                    }

                                    if (asset1order_eligibility && asset1no_assets!=0 && asset2order_eligibility && asset2no_assets != 0)
                                    {
                                        string maxalloc = (Convert.ToDecimal((asset1ask_quote * 100 * asset1no_assets)) + Convert.ToDecimal((asset1no_assets * Convert.ToDecimal(optioncommission)))).ToString();

                                         placeassetorder(asset2ticker, Math.Round(Convert.ToDouble(asset2strike),2).ToString(), asset2assettype, asset2no_assets, asset2asset_expiry_year, asset2asset_expiry_month, asset2asset_expiry_day, "BUY_OPEN", (double)Math.Round(Convert.ToDecimal(asset2ask_quote), 2));
                                         placeassetorder(asset1ticker, Math.Round(Convert.ToDouble(asset1strike),2).ToString(), asset1assettype, asset1no_assets, asset1asset_expiry_year, asset1asset_expiry_month, asset1asset_expiry_day, "BUY_OPEN", (double)Math.Round(Convert.ToDecimal(asset1ask_quote), 2));
 
                                        //Prices for tracking
                                        string cmd = "insert into BuyPrices values (" + Convert.ToDouble(assetid1) + "," + Convert.ToDouble(asset1ask_quote) + "," + Convert.ToDouble(asset1no_assets) + ","
                                       + Convert.ToDouble(maxalloc) + ",getdate())";

                                        inserintoDB(cmd);

                                        cmd = "insert into Log values (1," + Convert.ToDouble(assetid1) + "," + "'Purchase order for price $" + Convert.ToDouble(asset1ask_quote) + "',getdate())";
                                        inserintoDB(cmd);

                                        //Prices for tracking
                                        maxalloc = (Convert.ToDecimal((asset2ask_quote * 100 * asset2no_assets)) + Convert.ToDecimal((asset2no_assets * Convert.ToDecimal(optioncommission)))).ToString();
                                        cmd = "insert into BuyPrices values (" + Convert.ToDouble(assetid2) + "," + Convert.ToDouble(asset2ask_quote) + "," + Convert.ToDouble(asset2no_assets) + ","
                                       + Convert.ToDouble(maxalloc) + ",getdate())";

                                        inserintoDB(cmd);

                                        cmd = "insert into Log values (1," + Convert.ToDouble(assetid2) + "," + "'Purchase order for price $" + Convert.ToDouble(asset2ask_quote) + "',getdate())";
                                        inserintoDB(cmd);
                                   }
                                }

                            }

                        }

                    }        
                }
              }
              System.Threading.Thread.Sleep(10000);
           }
       }

        static string getoptimumprice(string assetid1,string assetid2,decimal asset1maxalloc,decimal asset2maxalloc,decimal principal)
        {

            ArrayList Assets = new ArrayList();
            Assets.AddRange(AssetInfo);
            decimal assetprice1 = 0;
            decimal assetprice2 = 0;
            string optimumpricestring = "";
            decimal remainderasset1 = 0;
            decimal remainderasset2 = 0;
            decimal remainder = 0;
            foreach (asset_information item in Assets)
            {
                if (item.assetid==assetid1)
                {
                    assetprice1 = Convert.ToDecimal(item.stock_lastask);
                }

                if (item.assetid == assetid2)
                {
                    assetprice2 = Convert.ToDecimal(item.stock_lastask);
                }

            }

            if (assetprice1!=0&&assetprice2!=0)
            {
            decimal adjustedasset1price = assetprice1 * 100;
            decimal adjustedasset2price = assetprice2 * 100;

            int num_optionsasset1 = (int)Math.Floor(asset1maxalloc/ adjustedasset1price);
            int num_optionsasset2 = (int)Math.Floor(asset2maxalloc / adjustedasset2price);


            if (num_optionsasset1!=0 && num_optionsasset2!=0)
                {
                    if (num_optionsasset1 == 1)
                    {
                        remainderasset1 = asset1maxalloc - Convert.ToDecimal(4.74) -  adjustedasset1price;
                    }
                    else
                    {
                        remainderasset1 = asset1maxalloc - Convert.ToDecimal(4.74) - ((num_optionsasset1 - 1) * Convert.ToDecimal(optioncommission)) - (num_optionsasset1 * adjustedasset1price);
                    }

                    if (remainderasset1 < 0)
                    {
                        while (remainderasset1 < 0)
                        {
                            remainderasset1 = 0;
                            num_optionsasset1 = num_optionsasset1 - 1;
                            if (num_optionsasset1 == 0)
                            {
                                return "";
                            }

                            if (num_optionsasset1== 1)
                            {
                                remainderasset1 = asset1maxalloc - Convert.ToDecimal(4.74) -adjustedasset1price;
                            }
                            else
                            {
                                remainderasset1 = asset1maxalloc - Convert.ToDecimal(4.74) - ((num_optionsasset1 - 1) * Convert.ToDecimal(optioncommission)) - (num_optionsasset1 * adjustedasset1price);
                            }
                        }

                    }

                    if (num_optionsasset2== 1)
                    {
                        remainderasset2 = asset2maxalloc - Convert.ToDecimal(4.74) - adjustedasset2price;
                    }
                    else
                    {
                        remainderasset2 = asset2maxalloc - Convert.ToDecimal(4.74) - ((num_optionsasset2 - 1) * Convert.ToDecimal(optioncommission)) - (num_optionsasset2 * adjustedasset2price);
                    }


                    if (remainderasset2 < 0)
                    {
                        while (remainderasset2 < 0)
                        {
                            remainderasset2 = 0;
                            num_optionsasset2 = num_optionsasset2 - 1;
                            if (num_optionsasset2 == 0)
                            {
                                return "";
                            }
                            if (num_optionsasset2 == 1)
                            {
                                remainderasset2 = asset2maxalloc - Convert.ToDecimal(4.74) - adjustedasset2price;
                            }
                            else
                            {
                                remainderasset2 = asset2maxalloc - Convert.ToDecimal(4.74) - ((num_optionsasset2 - 1) * Convert.ToDecimal(optioncommission)) - (num_optionsasset2 * adjustedasset2price);
                            }
                        }
                    }

                    if (remainderasset1 >= remainderasset2 && remainderasset1 > 0 && remainderasset2 > 0 && num_optionsasset1 > 0 && num_optionsasset2 > 0)
                    {
                        if ((((remainderasset1 / asset1maxalloc) * 100) - ((remainderasset2 / asset2maxalloc) * 100)) <= Convert.ToDecimal(maxpricedeviation))
                        {
                            optimumpricestring = num_optionsasset1 + "," + assetprice1 + ";" + num_optionsasset2 + "," + assetprice2;
                            return optimumpricestring;
                        }
                    }
                    else
                    {
                        if ((((remainderasset2 / asset2maxalloc) * 100) - ((remainderasset1 / asset1maxalloc) * 100)) <= Convert.ToDecimal(maxpricedeviation))
                        {
                            optimumpricestring = num_optionsasset1 + "," + assetprice1 + ";" + num_optionsasset2 + "," + assetprice2;
                            return optimumpricestring;
                        }

                    }

                    if (remainderasset1 >= remainderasset2 && remainderasset1 > 0 && remainderasset2 > 0 && num_optionsasset1>0 && num_optionsasset2>0)
                    {
                        decimal newprincipal = asset2maxalloc - remainderasset1;
                        if (newprincipal >= adjustedasset2price)
                        {
                            num_optionsasset2 = (int)Math.Floor(newprincipal / adjustedasset2price);
                            if (num_optionsasset2 != 0)
                            {
                                if (num_optionsasset2 == 1)
                                {
                                    remainder = newprincipal - Convert.ToDecimal(4.74) - adjustedasset2price;
                                }
                                else
                                {
                                    remainder = newprincipal - Convert.ToDecimal(4.74) - ((num_optionsasset2 - 1) * Convert.ToDecimal(optioncommission)) - (num_optionsasset2 * adjustedasset2price);
                                }

                                if (remainder < 0)
                                {
                                    while (remainder < 0)
                                    {
                                        remainder = 0;
                                        num_optionsasset2 = num_optionsasset2 - 1;
                                        if (num_optionsasset2 == 0)
                                        {
                                            return "";
                                        }
                                        if (num_optionsasset2 == 1)
                                        {
                                            remainder = newprincipal - Convert.ToDecimal(4.74) - adjustedasset2price;
                                        }
                                        else
                                        {
                                            remainder = newprincipal - Convert.ToDecimal(4.74) - ((num_optionsasset2 - 1) * Convert.ToDecimal(optioncommission)) - (num_optionsasset2 * adjustedasset2price);
                                        }
                                    }
                                }

                                if ((remainder / newprincipal) * 100 <= Convert.ToDecimal(maxpricedeviation))
                                {
                                    optimumpricestring = num_optionsasset1 + "," + assetprice1 + ";" + num_optionsasset2 + "," + assetprice2;
                                    return optimumpricestring;
                                }
                            }
                        }
                    }
                    else if (remainderasset1 < remainderasset2 && remainderasset1 > 0 && remainderasset2 > 0 && num_optionsasset1 > 0 && num_optionsasset2 > 0)
                    {
                        decimal newprincipal = asset1maxalloc - remainderasset2;
                        num_optionsasset1=(int)Math.Floor(newprincipal/adjustedasset1price);
                        if (num_optionsasset1 != 0)
                        {
                            if (num_optionsasset1 == 1)
                            {
                                remainder = newprincipal - Convert.ToDecimal(4.74) - adjustedasset1price;
                            }
                            else
                            {
                                remainder = newprincipal - Convert.ToDecimal(4.74) - ((num_optionsasset1 - 1) * Convert.ToDecimal(optioncommission)) - (num_optionsasset1 * adjustedasset1price);
                            }

                            if (remainder < 0)
                            {
                                while (remainder < 0)
                                {
                                    remainder = 0;
                                    num_optionsasset1 = num_optionsasset1 - 1;
                                    if (num_optionsasset1 == 0)
                                    {
                                        return "";
                                    }
                                    if (num_optionsasset1 == 1)
                                    {
                                        remainder = newprincipal - Convert.ToDecimal(4.74) - adjustedasset1price;
                                    }
                                    else
                                    {
                                        remainder = newprincipal - Convert.ToDecimal(4.74) - ((num_optionsasset1 - 1) * Convert.ToDecimal(optioncommission)) - (num_optionsasset1 * adjustedasset1price);
                                    }
                                }
                            }

                            if ((remainder / newprincipal) * 100 <= Convert.ToDecimal(maxpricedeviation))
                            {
                                optimumpricestring = num_optionsasset1 + "," + assetprice1 + ";" + num_optionsasset2 + "," + assetprice2;
                                return optimumpricestring;
                            }
                        }

                    }
            
                  }
        
            }

            return "";
        }

        public static bool assetordereligibility(string ticker, string assettype, string asset_expiry_day, string asset_expiry_month, string asset_expiry_year, int no_assets, double ask_quote, string ordertype, string strike)
        {
            double order_value = 0;
            string order_status = "";
            string asset_type = "";
            string asset_expirymonth = "";
            string asset_expiryyear = "";
            string asset_expiryday = "";
            string asset_strikeprice = "";
            string order_action = "";
            double order_quantity = 0;
            double estimated_commission = 0;
            double estimated_fees = 0;
            string symbol = "";
            bool eligible = true;
            double order_id = 0;

            System.Threading.Thread.Sleep(800);
            if (assettype == "1")
            {
                assettype = "PUT";
            }
            else
            {
                assettype = "CALL";
            }

            string account_number = ConfigurationManager.AppSettings["AccountNumber"];
            try
            {
                string orderlist_response = getorderlist(account_number);
                if (orderlist_response != "")
                {

                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(orderlist_response);
                    XmlNodeList nodes = doc.GetElementsByTagName("order");

                    if (nodes.Count > 0)
                    {
                        foreach (XmlNode node in nodes)
                        {

                            XmlNodeList ind_nodelist = node.ChildNodes;
                            foreach (XmlNode ind_node in ind_nodelist)
                            {
                                if (ind_node.Name.ToLower() == "orderid")
                                    order_id = Convert.ToDouble(ind_node.InnerText);
                                if (ind_node.Name.ToLower() == "ordervalue")
                                    order_value = Convert.ToDouble(ind_node.InnerText);
                                if (ind_node.Name.ToLower() == "orderstatus")
                                    order_status = ind_node.InnerText;
                                if (ind_node.Name.ToLower() == "legdetails")
                                {
                                    XmlNodeList ind_legdetailsnodelist = ind_node.ChildNodes[0].ChildNodes;
                                    foreach (XmlNode ind_legdetailsnode in ind_legdetailsnodelist)
                                    {
                                        if (ind_legdetailsnode.Name.ToLower() == "symbolinfo")
                                        {
                                            XmlNodeList ind_symbolnodelist = ind_legdetailsnode.ChildNodes;
                                            foreach (XmlNode ind_symbolnode in ind_symbolnodelist)
                                            {
                                                if (ind_symbolnode.Name.ToLower() == "symbol")
                                                    symbol = ind_symbolnode.InnerText;
                                                if (ind_symbolnode.Name.ToLower() == "callput")
                                                    asset_type = ind_symbolnode.InnerText;
                                                if (ind_symbolnode.Name.ToLower() == "expyear")
                                                    asset_expiryyear = ind_symbolnode.InnerText;
                                                if (ind_symbolnode.Name.ToLower() == "expmonth")
                                                    asset_expirymonth = ind_symbolnode.InnerText;
                                                if (ind_symbolnode.Name.ToLower() == "expday")
                                                    asset_expiryday = ind_symbolnode.InnerText;
                                                if (ind_symbolnode.Name.ToLower() == "strikeprice")
                                                    asset_strikeprice = ind_symbolnode.InnerText;
                                            }

                                        }

                                        if (ind_legdetailsnode.Name.ToLower() == "orderaction")
                                            order_action = ind_legdetailsnode.InnerText;
                                        if (ind_legdetailsnode.Name.ToLower() == "orderedquantity")
                                            order_quantity = Convert.ToDouble(ind_legdetailsnode.InnerText);
                                        if (ind_legdetailsnode.Name.ToLower() == "estimatedcommission")
                                            estimated_commission = Convert.ToDouble(ind_legdetailsnode.InnerText);
                                        if (ind_legdetailsnode.Name.ToLower() == "estimatedfees")
                                            estimated_fees = Convert.ToDouble(ind_legdetailsnode.InnerText);
                                    }
                                }

                            }

                            Dictionary<String, String> currentordervalues = new Dictionary<string, string>();
                            currentordervalues.Add("symbol", ticker);
                            currentordervalues.Add("callput", asset_type);
                            currentordervalues.Add("expyear", asset_expiry_year);
                            currentordervalues.Add("expmonth", asset_expiry_month);
                            currentordervalues.Add("expday", asset_expiry_day);
                            currentordervalues.Add("strikeprice", asset_strikeprice);
                            currentordervalues.Add("orderaction", order_action);
                            currentordervalues.Add("orderedquantity", no_assets.ToString());
                            currentordervalues.Add("orderid", order_id.ToString());
                            currentordervalues.Add("newprice", ask_quote.ToString());

                            if (ticker.ToLower() == symbol.ToLower() && assettype.ToLower() == asset_type.ToLower() && asset_expiry_day == asset_expiryday && asset_expiry_month == asset_expirymonth && asset_expiry_year == asset_expiryyear && order_action.ToLower().Trim() == ordertype.ToLower().Trim() && Convert.ToDouble(asset_strikeprice) == Convert.ToDouble(strike) && order_status.ToLower() != "cancelled" && order_status.ToLower() != "executed")
                            {
                                if (no_assets == Convert.ToInt32(Math.Round(Convert.ToDecimal(order_quantity), 0)) && Decimal.Round((decimal)ask_quote,2) == Decimal.Round((decimal)(order_value + estimated_commission + estimated_fees)/ (decimal)(order_quantity*100), 2))
                                {
                                    eligible = false;
                                    return eligible;
                                }

                                else
                                {
                         
                                    modifyassetorder(currentordervalues);
                                    eligible = false;
                                    return eligible;
                                }
                            }
                            else
                            {
                                eligible = true;
                            }

                        }

                        return eligible;
                    }
                    else
                    {
                        return eligible;
                    }
                }
                else
                {
                    eligible = false;
                    return eligible;

                }
            }

            catch (Exception ex)
            {
                string exp1 = ex.Message.Replace("'", "''");
                writelog("'Asset Eligibility: " + exp1 + "'");
                eligible = false;
                return eligible;
            }

        }

        public static void placeassetorder(string sym, string strike, string assettype, int qty, string expyr, string expmonth, string expday, string action, double limitprice)
        {
            Object newlock = new Object();
            lock (newlock)
            {
                System.Threading.Thread.Sleep(800);
                string access_token = "";
                string access_token_secret = "";
                String insertCmd = "Select top 1 tokenid, tokensecret from dbo.AccessToken order by creationdatetime desc";
                SqlCommand myCommand = new SqlCommand(insertCmd, thisConnection);
                SqlDataAdapter adapter = new SqlDataAdapter(myCommand);
                DataSet tokens = new DataSet();
                adapter.Fill(tokens);
                using (tokens)
                {
                    foreach (DataRow row in tokens.Tables[0].Rows)
                    {
                        access_token = row[0].ToString();
                        access_token_secret = row[1].ToString();
                    }
                }
                adapter.Dispose();

                Random _random = new Random();
                string clientorderid = "";
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < 10; i++)
                {
                    int g = _random.Next(3);
                    switch (g)
                    {
                        case 0:
                            // lowercase alpha
                            sb.Append((char)(_random.Next(26) + 97), 1);
                            break;
                        default:
                            // numeric digits
                            sb.Append((char)(_random.Next(10) + 48), 1);
                            break;
                    }
                }
                string oath_consumer = ConfigurationManager.AppSettings["oauth_consumer_key"];
                string oath_secret = ConfigurationManager.AppSettings["consumer_secret"];
                clientorderid = sb.ToString();
                string uri = ConfigurationManager.AppSettings["PlaceOrder"];
                string accountnumber = ConfigurationManager.AppSettings["AccountNumber"];
                if (assettype.Trim() == "1")
                {
                    assettype = "PUT";
                }
                else
                {
                    assettype = "CALL";
                }
                Manager accountmanager = new Manager(oath_consumer, oath_secret, access_token, access_token_secret);
                uri = uri.Trim();
                var authzHeader = accountmanager.GetAuthorizationHeader(uri, "POST");
                var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);
                request.Headers.Add("Authorization", authzHeader);
                request.Method = "POST";
                string file = ConfigurationManager.AppSettings["PlaceAssetOrderBaseXMLFile"];
                XmlDocument doc = new XmlDocument();
                doc.PreserveWhitespace = false;
                doc.Load(file);
                XmlNodeList nodes = doc.GetElementsByTagName("PlaceOptionOrder");
                foreach (XmlNode node in nodes)
                {

                    XmlNode ind_node = node.ChildNodes[0];
                    XmlNodeList mainnode = ind_node.ChildNodes;
                    foreach (XmlNode node1 in mainnode)
                    {
                        if (node1.Name.ToLower() == "accountid")
                        {
                            node1.InnerText = ConfigurationManager.AppSettings["AccountNumber"].Trim();
                        }

                        if (node1.Name.ToLower() == "clientorderid")
                        {
                            node1.InnerText = clientorderid.Trim();
                        }

                        if (node1.Name.ToLower() == "limitprice")
                        {
                            node1.InnerText = limitprice.ToString().Trim();
                        }

                        if (node1.Name.ToLower() == "quantity")
                        {
                            node1.InnerText = qty.ToString().Trim();
                        }

                        if (node1.Name.ToLower() == "orderaction")
                        {
                            node1.InnerText = action.Trim();
                        }


                        if (node1.Name.ToLower() == "previewid")
                        {
                            node1.InnerText = "";
                        }

                        if (node1.Name.ToLower() == "stopprice")
                        {
                            node1.InnerText = "";
                        }

                        if (node1.Name.ToLower() == "stoplimitprice")
                        {
                            node1.InnerText = "";
                        }


                        if (node1.Name.ToLower() == "reserveorder")
                        {
                            node1.InnerText = "";
                        }

                        if (node1.Name.ToLower() == "reservequantity")
                        {
                            node1.InnerText = "";
                        }

                        if (node1.Name.ToLower() == "symbolinfo")
                        {
                            XmlNodeList list1 = node1.ChildNodes;
                            foreach (XmlNode nodes1 in list1)
                            {
                                if (nodes1.Name.ToLower() == "symbol")
                                {
                                    nodes1.InnerText = sym;
                                }

                                if (nodes1.Name.ToLower() == "callorput")
                                {
                                    nodes1.InnerText = assettype;
                                }

                                if (nodes1.Name.ToLower() == "strikeprice")
                                {
                                    nodes1.InnerText = strike;
                                }


                                if (nodes1.Name.ToLower() == "expirationyear")
                                {
                                    nodes1.InnerText = expyr;
                                }

                                if (nodes1.Name.ToLower() == "expirationmonth")
                                {
                                    nodes1.InnerText = expmonth;
                                }

                                if (nodes1.Name.ToLower() == "expirationday")
                                {
                                    nodes1.InnerText = expday;
                                }


                            }

                        }

                    }

                }

                XmlTextWriter tw = new XmlTextWriter(file, Encoding.ASCII);
                try
                {
                    tw.Formatting = Formatting.Indented; //this preserves indentation
                    doc.Save(tw);
                }
                finally
                {

                    tw.Close();

                }
                string body1 = GetTextFromXMLFile(file);
                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(body1);
                Stream os = request.GetRequestStream();
                os.Write(bytes, 0, bytes.Length);
                try
                {
                    var response = (System.Net.HttpWebResponse)request.GetResponse();
                    Stream reader = response.GetResponseStream();
                    StreamReader accountlist = new StreamReader(reader);
                    string accountlist_response = accountlist.ReadToEnd();
                    return;
                }

                catch (Exception ex)
                {
                    string exp1 = ex.Message.Replace("'", "''");
                    writelog("'Place Asset Order: " + exp1 + "'");
                    return;


                }
            }

        }

        public static void modifyassetorder(Dictionary<String, String> ordervaues)
        {
            Object locker = new Object();
            lock (locker)
            {
                string access_token = "";
                string access_token_secret = "";
                string assettype = "";
                string symbol = "";
                string asset_expiryyear = "";
                string asset_expirymonth = "";
                string asset_expiryday = "";
                string asset_strikeprice = "";
                string order_action = "";
                string order_quantity = "";
                string order_id = "";
                string price = "";
                Random _random = new Random();
                string clientorderid = "";
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < 10; i++)
                {
                    int g = _random.Next(3);
                    switch (g)
                    {
                        case 0:
                            // lowercase alpha
                            sb.Append((char)(_random.Next(26) + 97), 1);
                            break;
                        default:
                            // numeric digits
                            sb.Append((char)(_random.Next(10) + 48), 1);
                            break;
                    }
                }

                clientorderid = sb.ToString();
                String insertCmd = "Select top 1 tokenid, tokensecret from dbo.AccessToken order by creationdatetime desc";
                SqlCommand myCommand = new SqlCommand(insertCmd, thisConnection);
                SqlDataAdapter adapter = new SqlDataAdapter(myCommand);
                DataSet tokens = new DataSet();
                adapter.Fill(tokens);
                using (tokens)
                {
                    foreach (DataRow row in tokens.Tables[0].Rows)
                    {
                        access_token = row[0].ToString();
                        access_token_secret = row[1].ToString();
                    }
                }
                adapter.Dispose();

                foreach (KeyValuePair<String, String> p in ordervaues)
                {
                    if (p.Key.ToLower() == "callput")
                    {
                        assettype = p.Value;

                    }
                    if (p.Key.ToLower() == "symbol")
                    {
                        symbol = p.Value;

                    }
                    if (p.Key.ToLower() == "expyear")
                    {
                        asset_expiryyear = p.Value;

                    }
                    if (p.Key.ToLower() == "expmonth")
                    {
                        asset_expirymonth = p.Value;

                    }
                    if (p.Key.ToLower() == "expday")
                    {
                        asset_expiryday = p.Value;

                    }
                    if (p.Key.ToLower() == "strikeprice")
                    {
                        asset_strikeprice = p.Value;

                    }
                    if (p.Key.ToLower() == "orderaction")
                    {
                        order_action = p.Value;

                    }
                    if (p.Key.ToLower() == "orderedquantity")
                    {
                        order_quantity = p.Value;

                    }
                    if (p.Key.ToLower() == "orderid")
                    {
                        order_id = p.Value;

                    }

                    if (p.Key.ToLower() == "newprice")
                    {
                        price = p.Value;

                    }
                }

                string oath_consumer = ConfigurationManager.AppSettings["oauth_consumer_key"];
                string oath_secret = ConfigurationManager.AppSettings["consumer_secret"];
                string uri = ConfigurationManager.AppSettings["ModifyOrder"];
                string accountnumber = ConfigurationManager.AppSettings["AccountNumber"];
                if (assettype == "1")
                {
                    assettype = "PUT";
                }
                else
                {
                    assettype = "CALL";
                }
                Manager accountmanager = new Manager(oath_consumer, oath_secret, access_token, access_token_secret);
                uri = uri.Trim();
                var authzHeader = accountmanager.GetAuthorizationHeader(uri, "POST");
                var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);
                request.Headers.Add("Authorization", authzHeader);
                request.Method = "POST";
                string file = ConfigurationManager.AppSettings["ModifyAssetOrderBaseXML"];
                XmlDocument doc = new XmlDocument();
                doc.PreserveWhitespace = false;
                doc.Load(file);
                XmlNodeList nodes = doc.GetElementsByTagName("placeChangeOptionOrder");
                foreach (XmlNode node in nodes)
                {

                    XmlNode ind_node = node.ChildNodes[0];
                    XmlNodeList mainnode = ind_node.ChildNodes;
                    foreach (XmlNode node1 in mainnode)
                    {
                        if (node1.Name.ToLower() == "accountid")
                        {
                            node1.InnerText = ConfigurationManager.AppSettings["AccountNumber"].Trim();
                        }

                        if (node1.Name.ToLower() == "ordernum")
                        {
                            node1.InnerText = order_id;
                        }


                        if (node1.Name.ToLower() == "limitprice")
                        {
                            node1.InnerText = price;
                        }

                        if (node1.Name.ToLower() == "clientorderid")
                        {
                            node1.InnerText = clientorderid.Trim();
                        }

                        if (node1.Name.ToLower() == "quantity")
                        {
                            node1.InnerText = order_quantity; ;
                        }
                    }
                }

                XmlTextWriter tw = new XmlTextWriter(file, Encoding.ASCII);
                try
                {
                   tw.Formatting = Formatting.Indented; //this preserves indentation
                   doc.PreserveWhitespace = false;
                   doc.Save(tw);
                }

                finally
                {

                    tw.Close();

                }


                string body = GetTextFromXMLFile(file);
                Regex regex = new Regex(@">\s*<");
                string cleanedXml = regex.Replace(body, "><");
                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(cleanedXml);
                Stream os = request.GetRequestStream();
                os.Write(bytes, 0, bytes.Length);
                try
                {
                    var response = (System.Net.HttpWebResponse)request.GetResponse();
                    Stream reader = response.GetResponseStream();
                    StreamReader modifyorder = new StreamReader(reader);
                    string modifyorder_response = modifyorder.ReadToEnd();
                    return;
                }

                catch (Exception ex)
                {
                    string exp1 = ex.Message.Replace("'", "''");
                    writelog("'Modifying Asset order: " + exp1 + "'");
                    return;
                }
            }

        }

        public static void cancelassetorder(string orderid)
        {
            Object locking = new Object();
            lock (locking)
            {
                string access_token = "";
                string access_token_secret = "";
                String insertCmd = "Select top 1 tokenid, tokensecret from dbo.AccessToken order by creationdatetime desc";
                SqlCommand myCommand = new SqlCommand(insertCmd, thisConnection);
                SqlDataAdapter adapter = new SqlDataAdapter(myCommand);
                DataSet tokens = new DataSet();
                adapter.Fill(tokens);

                using (tokens)
                {
                    foreach (DataRow row in tokens.Tables[0].Rows)
                    {
                        access_token = row[0].ToString();
                        access_token_secret = row[1].ToString();
                    }
                }

                adapter.Dispose();

                string oath_consumer = ConfigurationManager.AppSettings["oauth_consumer_key"];
                string oath_secret = ConfigurationManager.AppSettings["consumer_secret"];
                string uri = ConfigurationManager.AppSettings["CancelOrder"];
                string accountnumber = ConfigurationManager.AppSettings["AccountNumber"];

                Manager accountmanager = new Manager(oath_consumer, oath_secret, access_token, access_token_secret);
                uri = uri.Trim();
                var authzHeader = accountmanager.GetAuthorizationHeader(uri, "POST");
                var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);
                request.Headers.Add("Authorization", authzHeader);
                request.Method = "POST";
                string file = ConfigurationManager.AppSettings["CancelAssetOrderBaseXML"];
                XmlDocument doc = new XmlDocument();
                doc.PreserveWhitespace = false;
                doc.Load(file);
                XmlNodeList nodes = doc.GetElementsByTagName("cancelOrder");
                foreach (XmlNode node in nodes)
                {

                    XmlNode ind_node = node.ChildNodes[0];
                    XmlNodeList mainnode = ind_node.ChildNodes;
                    foreach (XmlNode node1 in mainnode)
                    {
                        if (node1.Name.ToLower() == "accountid")
                        {
                            node1.InnerText = ConfigurationManager.AppSettings["AccountNumber"].Trim();
                        }


                        if (node1.Name.ToLower() == "ordernum")
                        {
                            node1.InnerText = orderid;
                        }

                    }

                }

                XmlTextWriter tw = new XmlTextWriter(file, Encoding.ASCII);
                try
                {
                    tw.Formatting = Formatting.Indented; //this preserves indentation
                    doc.Save(tw);
                }
                finally
                {

                    tw.Close();

                }
                string body1 = GetTextFromXMLFile(file);
                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(body1);
                Stream os = request.GetRequestStream();
                os.Write(bytes, 0, bytes.Length);
                try
                {
                    var response = (System.Net.HttpWebResponse)request.GetResponse();
                    Stream reader = response.GetResponseStream();
                    StreamReader cancellist = new StreamReader(reader);
                    string cancelorder_response = cancellist.ReadToEnd();
                    return;
                }

                catch (Exception ex)
                {
                    string exp1 = ex.Message.Replace("'", "''");
                    writelog("'Cancel asset order: " + exp1 + "'");
                    return;
                }
            }

        }

        public static string GetTextFromXMLFile(string file)
        {
            StreamReader reader = new StreamReader(file);
            string ret = reader.ReadToEnd();
            reader.Close();
            return ret;
        }

        public static void sellassets(SqlConnection continuedconn1)
        {
            //Request to constantly place sell order for profit trigger
            while (stop_process)
            {
                try
                {
                    string source_asset = "";
                    string target_asset = "";
                    string asset1gainloss = "";
                    string asset2gainloss = "";
                    string positionquantity1 = "";
                    string askquantity1 = "";
                    string positionquantity2 = "";
                    string askquantity2 = "";
                    double effectivegainloss = 0;
                    string log = "";
                    bool zeroqty = false;
                    double totalgain = 0; 
                    asset_information asset1 = null;
                    asset_information asset2 = null;
                    bool asset1eligibility = false;
                    bool asset2eligibility = false;
                    bool portfolioeligibility = true;
                    ArrayList Positions = new ArrayList();
                    ArrayList Assets = new ArrayList();
                    DateTime creationdate1=new DateTime();
                    DateTime creationdate2=new DateTime();
                    System.Threading.Thread.Sleep(10000);
                    Assets.AddRange(AssetInfo);
                    Positions.AddRange(PositionsInfo);
                    if (Positions.Count >= 1)
                    {
                        if (Positions.Count <=6)
                        {
                            foreach (accountpositioninformation acct in Positions)
                            {
                                if (acct != null)
                                {
                                    if (acct.qty == 0)
                                    {
                                        zeroqty = true;
                                    }
                                }
                            }

                            if (!zeroqty)
                            {
                                foreach (accountpositioninformation acct in Positions)
                                {
                                    if (acct.gainloss != "0")
                                    {
                                        totalgain = totalgain + Convert.ToDouble(acct.gainloss);
                                    }
                                }

                            }

                            if (Math.Round(totalgain,2) >= Convert.ToDouble(ConfigurationManager.AppSettings["TotalPortfolioGainLoss"]))
                            {
                                foreach (accountpositioninformation acct in Positions)
                                {

                                    foreach (asset_information curr in Assets)
                                    {
                                        if (curr.assetid == acct.assetid)
                                        {
                                            if (curr.stock_lastbid != "0" && curr.stock_lastbid != "0.00")
                                            {
                                                bool asseteligibility = assetordereligibility(curr.stock_name, curr.option_type, curr.option_expiryday, curr.option_expirymonth, curr.option_expiryyear
                                                    , acct.qty, Convert.ToDouble(curr.stock_lastbid), "SELL_CLOSE", curr.stock_strikeprice);
                                                if (!asseteligibility)
                                                {
                                                    portfolioeligibility = false;
                                                }
                                            }
                                        }

                                    }
                                    

                                }

                                if (portfolioeligibility)
                                {

                                    foreach (accountpositioninformation acct in Positions)
                                    {
                                        foreach (asset_information curr in Assets)
                                        {
                                            if (acct.assetid == curr.assetid)
                                            {
                                                if (curr.stock_lastbid != "" && curr.stock_lastbid != "0.00" && curr.stock_lastbid != "0")
                                                {
                                                    placeassetorder(curr.stock_name, Math.Round(Convert.ToDouble(curr.stock_strikeprice), 2).ToString(), curr.option_type, acct.qty, curr.option_expiryyear, curr.option_expirymonth, curr.option_expiryday, "SELL_CLOSE", Math.Round(Convert.ToDouble(curr.stock_lastbid), 2));
                                                    System.Threading.Thread.Sleep(500);
                                                    log = "insert into Log values (2," + Convert.ToDouble(curr.assetid) + "," + "'Sell order for price $" + Convert.ToDouble(curr.stock_lastask) + "',getdate())";
                                                    inserintoDB(log);
                                                }
                                                
                                            }
                                        }

                                    }

                                }
                            }
                        }
                        System.Threading.Thread.Sleep(1000);
                        foreach (accountpositioninformation acct in Positions)
                        {
                            if (acct != null)
                            {
                                if (acct.assetid != "" || acct.assetid != null)
                                {
                                    string sql = "select ap.assetid1,ap.assetid2,app.quantity quantity1, app1.quantity quantity2,ap.maxprofit,a.ticker, a.assettype, a.strike, " +
                                    "a.expiry_year, a.expiry_month,a.expiry_day,a1.ticker, a1.assettype, a1.strike, a1.expiry_year, a1.expiry_month,a1.expiry_day,app.creation_date creation_date1,app1.creation_date creation_date2 "+ 
                                    "from dbo.AssetPair ap join dbo.Assets a on ap.assetid1=a.assetid join dbo.Assets a1 on ap.assetid2=a1.assetid left join dbo.AssetPurchasePrice app "+
                                    " on app.assetid=ap.assetid1 left join dbo.AssetPurchasePrice app1 on  app1.assetid=ap.assetid2 where assetid1=" + Convert.ToInt32(acct.assetid) + " or assetid2=" + Convert.ToInt32(acct.assetid) + " and a.totrade=1 and a1.totrade=1";

                                    SqlCommand cmd = new SqlCommand(sql, thisConnection);
                                    SqlDataAdapter adapter1 = new SqlDataAdapter(cmd);
                                    DataSet tokens1 = new DataSet();
                                    adapter1.Fill(tokens1);
                                    source_asset = acct.assetid;

                                    if (tokens1.Tables[0].Rows.Count >= 1)
                                    {
                                        if (tokens1.Tables[0].Rows[0].ItemArray[0].ToString() == acct.assetid)
                                        {
                                            target_asset = tokens1.Tables[0].Rows[0].ItemArray[1].ToString();
                                            positionquantity1 = tokens1.Tables[0].Rows[0].ItemArray[2].ToString();
                                            positionquantity2 = tokens1.Tables[0].Rows[0].ItemArray[3].ToString();
                                            if (tokens1.Tables[0].Rows[0].ItemArray[17].ToString() != "")
                                            {
                                                creationdate1 = Convert.ToDateTime(tokens1.Tables[0].Rows[0].ItemArray[17].ToString());
                                            }
                                            if (tokens1.Tables[0].Rows[0].ItemArray[18].ToString() != "")
                                            {
                                                creationdate2 = Convert.ToDateTime(tokens1.Tables[0].Rows[0].ItemArray[18].ToString());
                                            }
                                        }
                                        else
                                        {
                                            target_asset = tokens1.Tables[0].Rows[0].ItemArray[0].ToString();
                                            positionquantity1 = tokens1.Tables[0].Rows[0].ItemArray[3].ToString();
                                            positionquantity2= tokens1.Tables[0].Rows[0].ItemArray[2].ToString();
                                            if (tokens1.Tables[0].Rows[0].ItemArray[18].ToString() != "")
                                            {
                                                creationdate1 = Convert.ToDateTime(tokens1.Tables[0].Rows[0].ItemArray[18].ToString());
                                            }
                                            if (tokens1.Tables[0].Rows[0].ItemArray[17].ToString() != "")
                                            {
                                                creationdate2 = Convert.ToDateTime(tokens1.Tables[0].Rows[0].ItemArray[17].ToString());
                                            }
                                        }

                                        asset1gainloss = acct.gainloss;
                                        foreach (accountpositioninformation acct1 in Positions)
                                        {
                                            if (acct1.assetid == target_asset)
                                            {
                                                asset2gainloss = acct1.gainloss;
                                                break;
                                            }

                                        }

                                        DateTime now = DateTime.Now;
                                        if (asset2gainloss != "" && asset1gainloss != "" && positionquantity1 != "" && positionquantity2!="")
                                        {

                                            if (asset1gainloss == "0")
                                            {
                                                effectivegainloss = Math.Round(Convert.ToDouble(asset2gainloss), 2);
                                            }
                                            else
                                            {
                                                effectivegainloss = Math.Round(Convert.ToDouble(asset1gainloss) + Convert.ToDouble(asset2gainloss), 2);
                                            }

                                            foreach (asset_information curr in Assets)
                                            {
                                                if (curr.assetid == source_asset)
                                                {
                                                    asset1 = curr;
                                                    askquantity1 = curr.stock_bidsize;
                                                }

                                                if (curr.assetid == target_asset)
                                                {
                                                    asset2 = curr;
                                                    askquantity2 = curr.stock_bidsize;
                                                }

                                            }

                               

                                            if ((asset1.stock_lastbid != "0.00" || asset1.stock_lastbid != "0") && (asset2.stock_lastbid != "0.00" || asset2.stock_lastbid != "0") && tokens1.Tables[0].Rows[0].ItemArray[3].ToString()!="" && tokens1.Tables[0].Rows[0].ItemArray[2].ToString()!="")
                                            {
                                                asset1eligibility = assetordereligibility(asset1.stock_name, asset1.option_type, asset1.option_expiryday, asset1.option_expirymonth, asset1.option_expiryyear, Convert.ToInt32(tokens1.Tables[0].Rows[0].ItemArray[2].ToString()), Convert.ToDouble(asset1.stock_lastbid), "SELL_CLOSE", asset1.stock_strikeprice);
                                                asset2eligibility = assetordereligibility(asset2.stock_name, asset2.option_type, asset2.option_expiryday, asset2.option_expirymonth, asset2.option_expiryyear, Convert.ToInt32(tokens1.Tables[0].Rows[0].ItemArray[3].ToString()), Convert.ToDouble(asset2.stock_lastbid), "SELL_CLOSE", asset2.stock_strikeprice);

                                                if (effectivegainloss >= Convert.ToDouble(tokens1.Tables[0].Rows[0].ItemArray[4]) && asset1eligibility && Convert.ToInt32(askquantity1) >= Convert.ToInt32(positionquantity1) && asset2eligibility && Convert.ToInt32(askquantity2) >= Convert.ToInt32(positionquantity2))
                                                {
                                                    placeassetorder(asset1.stock_name, Math.Round(Convert.ToDouble(asset1.stock_strikeprice), 2).ToString(), asset1.option_type, Convert.ToInt32(positionquantity1), asset1.option_expiryyear, asset1.option_expirymonth, asset1.option_expiryday, "SELL_CLOSE", Math.Round(Convert.ToDouble(asset1.stock_lastbid), 2));
                                                    placeassetorder(asset2.stock_name, Math.Round(Convert.ToDouble(asset2.stock_strikeprice), 2).ToString(), asset2.option_type, Convert.ToInt32(positionquantity2), asset2.option_expiryyear, asset2.option_expirymonth, asset2.option_expiryday, "SELL_CLOSE", Math.Round(Convert.ToDouble(asset2.stock_lastbid), 2));

                                                    log = "insert into Log values (2," + Convert.ToDouble(asset1.assetid) + "," + "'Sell order for price $" + Convert.ToDouble(asset1.stock_lastask) + "',getdate())";
                                                    inserintoDB(log);

                                                    log = "insert into Log values (2," + Convert.ToDouble(asset2.assetid) + "," + "'Sell order for price $" + Convert.ToDouble(asset2.stock_lastask) + "',getdate())";
                                                    inserintoDB(log);

                                                }
                                            }

                                        }
                                        else if (positionquantity1 == "" && positionquantity2!="")
                                        {

                                           TimeSpan sp = now.Subtract(creationdate2);
                                           int spsec = sp.Seconds;
                                           int spmin = sp.Minutes;
                                           int sphour = sp.Hours;
                                           int sptotal = spsec + (60 * spmin) + (60 * 60 * sp.Hours);
                                           if (sptotal >= Convert.ToInt32(ConfigurationManager.AppSettings["AllowableTimeafterBuy"]))
                                           {
                                               foreach (asset_information curr in Assets)
                                               {
                                                   if (curr.assetid == target_asset)
                                                   {
                                                       asset2 = curr;
                                                       askquantity2 = curr.stock_bidsize;

                                                   }
                                               }

                                               if (asset2.stock_lastbid != "" && asset2.stock_lastbid != "0" && asset2.stock_lastbid != "0.00")
                                               {
                                                   //Partially executed sell orders. Other asset in the pair was sold
                                                   asset2eligibility = assetordereligibility(asset2.stock_name, asset2.option_type, asset2.option_expiryday, asset2.option_expirymonth, asset2.option_expiryyear, Convert.ToInt32(positionquantity2), Convert.ToDouble(asset2.stock_lastbid), "SELL_CLOSE", asset2.stock_strikeprice);
                                                   if (Convert.ToInt32(askquantity2) >= Convert.ToInt32(positionquantity2) && asset2eligibility)
                                                   {
                                                       placeassetorder(asset2.stock_name, Math.Round(Convert.ToDouble(asset2.stock_strikeprice), 2).ToString(), asset2.option_type, Convert.ToInt32(positionquantity2), asset2.option_expiryyear, asset2.option_expirymonth, asset2.option_expiryday, "SELL_CLOSE", Math.Round(Convert.ToDouble(asset2.stock_lastbid), 2));
                                                       log = "insert into Log values (2," + Convert.ToDouble(asset2.assetid) + "," + "'Sell order for price $" + Convert.ToDouble(asset2.stock_lastask) + "',getdate())";
                                                       inserintoDB(log);
                                                   }
                                               }
                                           }
                                        }

                                        else if (positionquantity2 == "" && positionquantity1 != "")
                                        {
                                           TimeSpan sp = now.Subtract(creationdate1);
                                           int spsec = sp.Seconds;
                                           int spmin = sp.Minutes;
                                           int sphour = sp.Hours;
                                           int sptotal = spsec + (60 * spmin) + (60 * 60 * sp.Hours);
                                           if (sptotal >= Convert.ToInt32(ConfigurationManager.AppSettings["AllowableTimeafterBuy"]))
                                           {

                                               foreach (asset_information curr in Assets)
                                               {
                                                   if (curr.assetid == source_asset)
                                                   {
                                                       asset1 = curr;
                                                       askquantity1 = curr.stock_bidsize;

                                                   }
                                               }

                                               if (asset1.stock_lastbid != "" && asset1.stock_lastbid != "0" && asset1.stock_lastbid != "0.00")
                                               {
                                                   //Partially executed sell orders. Other asset in the pair was sold
                                                   asset1eligibility = assetordereligibility(asset1.stock_name, asset1.option_type, asset1.option_expiryday, asset1.option_expirymonth, asset1.option_expiryyear, Convert.ToInt32(positionquantity1), Convert.ToDouble(asset1.stock_lastbid), "SELL_CLOSE", asset1.stock_strikeprice);
                                                   if (Convert.ToInt32(askquantity1) >= Convert.ToInt32(positionquantity1) && asset1eligibility)
                                                   {
                                                       placeassetorder(asset1.stock_name, Math.Round(Convert.ToDouble(asset1.stock_strikeprice), 2).ToString(), asset1.option_type, Convert.ToInt32(positionquantity1), asset1.option_expiryyear, asset1.option_expirymonth, asset1.option_expiryday, "SELL_CLOSE", Math.Round(Convert.ToDouble(asset1.stock_lastbid), 2));
                                                       log = "insert into Log values (2," + Convert.ToDouble(asset1.assetid) + "," + "'Sell order for price $" + Convert.ToDouble(asset1.stock_lastask) + "',getdate())";
                                                       inserintoDB(log);
                                                   }
                                               }
                                           }
                                        }

                                    }
                                }

                            }
                        }
                   
                    }
                }

                catch (Exception ex)
                {
                    string exp1 = ex.Message.Replace("'", "''");
                    writelog("'Sell Assets: " + exp1 + "'");
                    sellassets(thisConnection);
                }

            }

        }

        public  string getgainloss(string purchase, string assetid)
        {
            double gainloss = 0;
            ArrayList Assets = new ArrayList();
            Assets.AddRange(AssetInfo);

            foreach (asset_information asset in Assets)
            {
                if (asset.assetid == assetid)
                {
                    if (Convert.ToDouble(asset.stock_lastprice) != 0)
                    {
                        gainloss = Math.Round(((Convert.ToDouble(asset.stock_lastprice) - Convert.ToDouble(purchase)) / Convert.ToDouble(purchase)) * 100, 2);
                        return gainloss.ToString();
                    }

                }
            }


            return gainloss.ToString();
        }

        public static string getorderlist(string account_number)
        {
            System.Threading.Thread.Sleep(800);
            string access_token = "";
            string access_token_secret = "";
            string order_number = "";
            string AssetInfo = "";
            string OrderType="";
            string OrderStatus="";
            string OrderCost="";
            int Quantity=0;
            string OrderPlacedTime = "";
            string OrderExecutedTime = "";
            string symbol = "";
            string asset_type = "";
            string asset_expirymonth = "";
            string asset_expiryday = "";
            string asset_strikeprice = "";

            //long baseTicks = 621355968000000000;
            //long tickResolution = 10000000;
            //long orderplacedepoc=0;
            //long orderexecutedepoch=0;
            //long epochTicks = 0;
            String insertCmd = "Select top 1 tokenid, tokensecret from dbo.AccessToken order by creationdatetime desc";
            SqlCommand myCommand = new SqlCommand(insertCmd, thisConnection);
            SqlDataAdapter adapter = new SqlDataAdapter(myCommand);
            DataSet tokens = new DataSet();
            adapter.Fill(tokens);
            using (tokens)
            {
                foreach (DataRow row in tokens.Tables[0].Rows)
                {
                    access_token = row[0].ToString();
                    access_token_secret = row[1].ToString();
                }
            }
            adapter.Dispose();
            string oath_consumer = ConfigurationManager.AppSettings["oauth_consumer_key"];
            string oath_secret = ConfigurationManager.AppSettings["consumer_secret"];
            string uri = ConfigurationManager.AppSettings["GetOrderList"];
            //Request to constantly poll the stocks and strike prices in question'
            Manager accountmanager = new Manager(oath_consumer, oath_secret, access_token, access_token_secret);
            uri = uri + account_number;
            var authzHeader = accountmanager.GetAuthorizationHeader(uri, "GET");

            //prepare the token request
            var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);
            request.Headers.Add("Authorization", authzHeader);
            request.Method = "GET";

            try
            {
                var response = (System.Net.HttpWebResponse)request.GetResponse();
                Stream reader = response.GetResponseStream();
                StreamReader orderlist = new StreamReader(reader);
                string orderlist_response = orderlist.ReadToEnd();
                if (orderlist_response != "")
                {

                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(orderlist_response);
                    XmlNodeList nodes = doc.GetElementsByTagName("order");
                    if (nodes.Count > 0)
                    {
                        OrdersInfo.Clear();
                        foreach (XmlNode node in nodes)
                        {

                            XmlNodeList ind_nodelist = node.ChildNodes;
                            foreach (XmlNode ind_node in ind_nodelist)
                            {
                                if (ind_node.Name.ToLower() == "orderid")
                                    order_number = ind_node.InnerText;
                                //if (ind_node.Name.ToLower() == "orderplacedtime")
                                //{
                                //    orderplacedepoc = Convert.ToInt64(ind_node.InnerText);
                                //    epochTicks = (orderplacedepoc * tickResolution) + baseTicks;
                                //    OrderPlacedTime = new DateTime(epochTicks, DateTimeKind.Utc).ToString();
                                //}
                                //if (ind_node.Name.ToLower() == "orderexecutedtime")
                                //{
                                //    orderexecutedepoch = Convert.ToInt64(ind_node.InnerText);
                                //    epochTicks = (orderexecutedepoch * tickResolution) + baseTicks;
                                //    OrderExecutedTime = new DateTime(epochTicks, DateTimeKind.Utc).ToString();
                                //}
                                if (ind_node.Name.ToLower() == "ordervalue")
                                {
                                    OrderCost = ind_node.InnerText;
                                }
                                if (ind_node.Name.ToLower() == "orderstatus")
                                {
                                    OrderStatus = ind_node.InnerText;
                                }

                                if (ind_node.Name.ToLower() == "legdetails")
                                {
                                    XmlNodeList ind_legdetailsnodelist = ind_node.ChildNodes[0].ChildNodes;
                                    foreach (XmlNode ind_legdetailsnode in ind_legdetailsnodelist)
                                    {
                                        if (ind_legdetailsnode.Name.ToLower() == "symbolinfo")
                                        {
                                            XmlNodeList ind_symbolnodelist = ind_legdetailsnode.ChildNodes;
                                            foreach (XmlNode ind_symbolnode in ind_symbolnodelist)
                                            {
                                                if (ind_symbolnode.Name.ToLower() == "symbol")
                                                    symbol = ind_symbolnode.InnerText;
                                                if (ind_symbolnode.Name.ToLower() == "callput")
                                                    asset_type = ind_symbolnode.InnerText;
                                                if (ind_symbolnode.Name.ToLower() == "expmonth")
                                                    asset_expirymonth = ind_symbolnode.InnerText;
                                                if (ind_symbolnode.Name.ToLower() == "expday")
                                                    asset_expiryday = ind_symbolnode.InnerText;
                                                if (ind_symbolnode.Name.ToLower() == "strikeprice")
                                                    asset_strikeprice = ind_symbolnode.InnerText;
                                            }
                                            AssetInfo = symbol + " " + asset_strikeprice + " " + asset_type + " " + asset_expirymonth + " " + asset_expiryday;
                                        }

                                        if (ind_legdetailsnode.Name.ToLower() == "orderaction")
                                            OrderType = ind_legdetailsnode.InnerText;
                                        if (ind_legdetailsnode.Name.ToLower() == "orderedquantity")
                                            Quantity = Convert.ToInt32(ind_legdetailsnode.InnerText);
                                    }
                                }

                            }

                            orderinformation order = new orderinformation();
                            order.OrderNumber = order_number;
                            order.AssetInfo = AssetInfo;
                            order.OrderType = OrderType;
                            order.OrderCost = OrderCost;
                            order.OrderStatus = OrderStatus;
                            order.Quantity = Quantity;
                            order.OrderPlacedTime = OrderPlacedTime;
                            order.OrderExecutedTime = OrderExecutedTime;
                            OrdersInfo.Add(order);
                        }
                    }

                    return orderlist_response;
                }
                return "";
            }

            catch (Exception ex)
            {
                string exp1 = ex.Message.Replace("'", "''");
                writelog("'Get Order List: " + exp1 + "'");
                return "";
            }
        }

        public  void renewaccesstoken(SqlConnection connect)
        {
            string duration = ConfigurationManager.AppSettings["AccessTokenDuration"];
            while (stop_process)
            {
                string time = System.DateTime.Now.ToString();
                DateTime timenow = DateTime.Now;
                string timenow1 = timenow.ToString("H:mm");
                string[] nowtime = timenow1.Split(':');
                string hournow = nowtime[0];
                string minutenow = nowtime[1];
                double totaltimenow = Math.Round(Convert.ToDouble(hournow) * 60 * 60 + Convert.ToDouble(minutenow) * 60, 0);
                string[] timelast = lastaccesstokentime.Split(':');
                string hourlast = timelast[0];
                string minutelast = timelast[1];
                double totallasttime = Math.Round(Convert.ToDouble(hourlast) * 60 * 60 + Convert.ToDouble(minutelast) * 60, 0);
                if ((totaltimenow - totallasttime) > Convert.ToDouble(duration))
                {
                    string oath_consumer = ConfigurationManager.AppSettings["oauth_consumer_key"];
                    string oath_secret = ConfigurationManager.AppSettings["consumer_secret"];
                    string uri = ConfigurationManager.AppSettings["RenewAccessToken"];
                    string access_token = "";
                    string access_token_secret = "";

                    String insertCmd = "Select top 1 tokenid, tokensecret from dbo.AccessToken order by creationdatetime desc";
                    SqlCommand myCommand = new SqlCommand(insertCmd, connect);
                    SqlDataAdapter adapter = new SqlDataAdapter(myCommand);
                    DataSet tokens = new DataSet();
                    adapter.Fill(tokens);
                    using (tokens)
                    {
                        foreach (DataRow row in tokens.Tables[0].Rows)
                        {
                            access_token = row[0].ToString();
                            access_token_secret = row[1].ToString();
                        }
                    }
                    adapter.Dispose();

                    //Request to constantly poll the stocks and strike prices in question'
                    Manager accountmanager = new Manager(oath_consumer, oath_secret, access_token, access_token_secret);
                    var authzHeader = accountmanager.GetAuthorizationHeader(uri, "GET");

                    //prepare the token request
                    var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);
                    request.Headers.Add("Authorization", authzHeader);
                    request.Method = "GET";

                    try
                    {
                        using (var response = (System.Net.HttpWebResponse)request.GetResponse())
                        {
                            Stream reader = response.GetResponseStream();
                            StreamReader renewedtoken = new StreamReader(reader);
                            string renewtoken_response = renewedtoken.ReadToEnd();
                        }
                        DateTime renewed = DateTime.Now;
                        string timenow11 = renewed.ToString("H:mm");
                        lastaccesstokentime = timenow11;
                        System.Threading.Thread.Sleep(10000);
                    }

                    catch (Exception ex)
                    {
                        string exp1 = ex.Message.Replace("'", "''");
                        writelog("'Renew Access Token: " + exp1 + "'");
                        renewaccesstoken(thisConnection);
                    }
                }

                System.Threading.Thread.Sleep(3000);
            }
        }

        private void simpleButton3_Click(object sender, EventArgs e)
        {
            if (stop_process)
            {
                simpleButton3.Enabled = false;
                string verifier = textEdit1.Text;
                string access_token = ConfigurationManager.AppSettings["AccessTokenURL"];
                OAuthResponse new_response = oauth.AcquireAccessToken(access_token, "POST", verifier);
                labelControl6.Text="Access Token: " + oauth["token"];
                labelControl7.Text="Access Token Secret: " + oauth["token_secret"];
                String insertCmd = "insert into dbo.AccessToken values ('" + oauth["token"] + "','" + oauth["token_secret"] + "',getdate())";
                SqlCommand myCommand = new SqlCommand(insertCmd, thisConnection);
                myCommand.ExecuteNonQuery();
                labelControl4.Text = "Bot authenticated and logged in..Ready to begin..";
                labelControl4.Refresh();
                DateTime timenow = DateTime.Now;
                string time = timenow.ToString("H:mm");
                lastaccesstokentime = time;
                Thread thread = new Thread(new ThreadStart(checkendtime));
                thread.Start();
                System.Threading.Thread.Sleep(1000);
                var t = new Thread(() => renewaccesstoken(thisConnection));
                t.Start();
                var t1 = new Thread(() => runlooping(thisConnection));
                t1.Start();
            }
        }

        private void simpleButton4_Click(object sender, EventArgs e)
        {
            simpleButton1.Enabled = true;
            simpleButton2.Enabled = true;
            simpleButton5.Enabled = true;
            panel3.SendToBack();
            panel3.Visible = false;
            panel1.BringToFront();
            panel2.BringToFront();
            simpleButton4.Enabled = false;

            GraphicalDisplay s = new GraphicalDisplay();
            s.thisConnection = thisConnection;
            CheckMdiChildren(s);
           
        }

        public void CheckMdiChildren(Form form)
        {
            foreach (Form frm in this.MdiChildren)
                if (frm.GetType() == form.GetType())
                {
                    frm.Focus();
                    return;
                }

            form.MdiParent = this;
            form.Show();
        }

        private void simpleButton2_Click(object sender, EventArgs e)
        {
            simpleButton1.Enabled = true;
            simpleButton4.Enabled = true;
            simpleButton2.Enabled = false;
            panel3.Visible = false;
            simpleButton5.Enabled = true;
            panel1.BringToFront();
            panel2.BringToFront();
            AssetMonitor s = new AssetMonitor();
            s.thiconnect = thisConnection;
            CheckMdiChildren(s);
        }

        private void MainEntry_FormClosing(object sender, FormClosingEventArgs e)
        {
            Application.Exit();
        }

        private void simpleButton5_Click(object sender, EventArgs e)
        {
            simpleButton1.Enabled = true;
            simpleButton4.Enabled = true;
            simpleButton2.Enabled = true;
            simpleButton5.Enabled = false;
            panel3.Visible = false;
            panel1.BringToFront();
            panel2.BringToFront();
            PerformanceView s = new PerformanceView();
            s.thisconnect = thisConnection;
            CheckMdiChildren(s);
        }

        private void simpleButton6_Click(object sender, EventArgs e)
        {
            if (simpleButton6.Text.ToString().ToLower() == "begin bot")
            {

                string request_token = "";
                string token_authorize = "";
                string access_token = "";
                string oath_consumer = ConfigurationManager.AppSettings["oauth_consumer_key"];
                string oath_secret = ConfigurationManager.AppSettings["consumer_secret"];



                request_token = ConfigurationManager.AppSettings["RequestTokenURL"];
                token_authorize = ConfigurationManager.AppSettings["AuthorizeTokenURL"];
                access_token = ConfigurationManager.AppSettings["AccessTokenURL"];
                //Request to login and authenticate

                oauth["consumer_key"] = oath_consumer;
                oauth["consumer_secret"] = oath_secret;
                OAuthResponse response = oauth.AcquireRequestToken(request_token, "POST");
                string authorize_request = token_authorize + oauth["consumer_key"] + "&token=" + oauth["token"];
                System.Diagnostics.Process.Start(authorize_request);
                labelControl4.Text = "Enter verification code from browser";
                simpleButton6.Text = "Stop Bot";
            }
            else
            {
                stop_process = false;
                KillProcess("iexplore");
                simpleButton6.Text = "Begin Bot";
            }

        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            panel3.BringToFront();
            panel3.Visible = true;
            simpleButton1.Enabled = true;
            simpleButton2.Enabled = true;
            simpleButton4.Enabled = true;
            simpleButton5.Enabled = true;
        }

        public string getassetfromid(string asset)
        {
            string assetname = "";
            string opttype="";
            foreach (asset_information item in AssetInfo)
            {
                if (item.assetid == asset)
                {
                    if (item.option_type == "1")
                    {
                        opttype = "PUT";
                    }
                    else
                    {
                        opttype = "CALL";
                    }

                    assetname = item.stock_name + " " + item.stock_strikeprice + " " + opttype + " " + item.option_expirymonth + " " + item.option_expiryday;
                    return assetname;
                }
            }
            return assetname;
        }

        public void callorders()
        {
            string account_number = ConfigurationManager.AppSettings["AccountNumber"];
            while (stop_process)
            {
                getorderlist(account_number);
                System.Threading.Thread.Sleep(10000);
            }
        }
     
    }

    public class asset_information
    {
        public int id;
        public string assetid;
        public string to_trade;
        public string stock_name;
        public string option_expirymonth;
        public string option_expiryday;
        public string option_expiryyear;
        public string stock_strikeprice;
        public string option_type;
        public string stock_lastprice;
        public string stock_lastbid;
        public string stock_lastask;
        public string stock_asksize;
        public string stock_bidsize;
        public string stock_orderplaced;
        public string stock_purchaseprice;
        public string stock_volume;

        public double getprofitloss()
        {
            double profitloss;
            profitloss = Math.Round(((Convert.ToDouble(this.stock_purchaseprice) - Convert.ToDouble(this.stock_lastask)) / Convert.ToDouble(this.stock_purchaseprice)) * 100, 2);
            return profitloss;
        }

    }

    public class accountbalanceinformation
    {
        public string account_number;
        public string latest_available_balance;
        public string margin_purchasing_power;
        public string available_margincash;

    }

    public class accountpositioninformation
    {
        public string account_number;
        public string assetid;
        public string asset;
        public string asset_type;
        public string purchaseprice;
        public string gainloss;
        public int qty;
        public string TotalCapitalAllocation;
    }

    public class orderinformation
    {
        public string OrderNumber;
        public string AssetInfo;
        public string OrderType;
        public string OrderStatus;
        public string OrderCost;
        public int Quantity;
        public string OrderPlacedTime;
        public string OrderExecutedTime;

    }

    public class Manager
    {

        public Manager()
        {
            _random = new Random();
            _params = new Dictionary<String, String>();
            _params["callback"] = "oob"; // presume "desktop" consumer
            _params["consumer_key"] = "";
            _params["consumer_secret"] = "";
            _params["timestamp"] = GenerateTimeStamp();
            _params["nonce"] = GenerateNonce();
            _params["signature_method"] = "HMAC-SHA1";
            _params["signature"] = "";
            _params["token"] = "";
            _params["token_secret"] = "";
            _params["version"] = "1.0";
        }

        public Manager(string consumerKey,
                       string consumerSecret,
                       string token,
                       string tokenSecret)
            : this()
        {
            _params["consumer_key"] = consumerKey;
            _params["consumer_secret"] = consumerSecret;
            _params["token"] = token;
            _params["token_secret"] = tokenSecret;
        }

        public string this[string ix]
        {
            get
            {
                if (_params.ContainsKey(ix))
                    return _params[ix];
                throw new ArgumentException(ix);
            }
            set
            {
                if (!_params.ContainsKey(ix))
                    throw new ArgumentException(ix);
                _params[ix] = value;
            }
        }

        public string GenerateTimeStamp()
        {
            TimeSpan ts = DateTime.UtcNow - _epoch;
            return Convert.ToInt64(ts.TotalSeconds).ToString();
        }

        public void NewRequest()
        {
            _params["nonce"] = GenerateNonce();
            _params["timestamp"] = GenerateTimeStamp();
        }

        public string GenerateNonce()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 8; i++)
            {
                int g = _random.Next(3);
                switch (g)
                {
                    case 0:
                        // lowercase alpha
                        sb.Append((char)(_random.Next(26) + 97), 1);
                        break;
                    default:
                        // numeric digits
                        sb.Append((char)(_random.Next(10) + 48), 1);
                        break;
                }
            }
            return sb.ToString();
        }

        public Dictionary<String, String> ExtractQueryParameters(string queryString)
        {
            if (queryString.StartsWith("?"))
                queryString = queryString.Remove(0, 1);

            var result = new Dictionary<String, String>();

            if (string.IsNullOrEmpty(queryString))
                return result;

            foreach (string s in queryString.Split('&'))
            {
                if (!string.IsNullOrEmpty(s) && !s.StartsWith("oauth_"))
                {
                    if (s.IndexOf('=') > -1)
                    {
                        string[] temp = s.Split('=');
                        result.Add(temp[0], temp[1]);
                    }
                    else
                        result.Add(s, string.Empty);
                }
            }

            return result;
        }

        public static string UrlEncode(string value)
        {
            var result = new System.Text.StringBuilder();
            foreach (char symbol in value)
            {
                if (unreservedChars.IndexOf(symbol) != -1)
                    result.Append(symbol);
                else
                    result.Append('%' + String.Format("{0:X2}", (int)symbol));
            }
            return result.ToString();
        }

        public static string unreservedChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_.~";

        public static string EncodeRequestParameters(ICollection<KeyValuePair<String, String>> p)
        {
            var sb = new System.Text.StringBuilder();
            foreach (KeyValuePair<String, String> item in p.OrderBy(x => x.Key))
            {
                if (!String.IsNullOrEmpty(item.Value) &&
                  !item.Key.EndsWith("secret") && !item.Key.EndsWith("token")//)
                    && !item.Key.EndsWith("accountId") && !item.Key.EndsWith("clientOrderId")
                    && !item.Key.EndsWith("limitPrice") && !item.Key.EndsWith("quantity")
                    && !item.Key.EndsWith("symbol") && !item.Key.EndsWith("callOrPut")
                    && !item.Key.EndsWith("strikePrice") && !item.Key.EndsWith("expirationYear")
                    && !item.Key.EndsWith("expirationMonth") && !item.Key.EndsWith("expirationDay")
                    && !item.Key.EndsWith("orderAction") && !item.Key.EndsWith("priceType")
                    && !item.Key.EndsWith("orderTerm") && !item.Key.EndsWith("allOrNone")
                    && !item.Key.EndsWith("routingDestination"))
                {
                    sb.AppendFormat("oauth_{0}=\"{1}\", ",
                                    item.Key,
                                    UrlEncode(item.Value));
                    //   sb.Append(item.Key + "=" + UrlEncode(item.Value));
                }
                else if (!String.IsNullOrEmpty(item.Value) &&
                    item.Key.EndsWith("token"))
                {
                    sb.AppendFormat("oauth_{0}=\"{1}\", ",
                     item.Key, item.Value);
                }
                else if (!String.IsNullOrEmpty(item.Value) && !item.Key.EndsWith("secret") && !item.Key.EndsWith("token")
                     && (item.Key.EndsWith("accountId") || item.Key.EndsWith("clientOrderId")
                     || item.Key.EndsWith("limitPrice") || item.Key.EndsWith("quantity")
                     || item.Key.EndsWith("symbol") || item.Key.EndsWith("callOrPut")
                     || item.Key.EndsWith("strikePrice") || item.Key.EndsWith("expirationYear")
                     || item.Key.EndsWith("expirationMonth") || item.Key.EndsWith("expirationDay")
                     || item.Key.EndsWith("orderAction") || item.Key.EndsWith("priceType")
                     || item.Key.EndsWith("orderTerm") || item.Key.EndsWith("allOrNone")
                     || item.Key.EndsWith("routingDestination")))
                {
                    // sb.Append(","+item.Key+"="+ item.Value);
                    sb.AppendFormat("{0}=\"{1}\", ", item.Key, UrlEncode(item.Value));
                }
            }

            return sb.ToString().TrimEnd(' ').TrimEnd(',');
        }

        public OAuthResponse AcquireRequestToken(string uri, string method)
        {
            NewRequest();
            var authzHeader = GetAuthorizationHeader(uri, method);

            // prepare the token request
            var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);
            request.Headers.Add("Authorization", authzHeader);
            request.Method = method;
            using (var response = (System.Net.HttpWebResponse)request.GetResponse())
            {
                using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                {
                    var r = new OAuthResponse(reader.ReadToEnd());
                    this["token"] = r["oauth_token"];

                    // Sometimes the request_token URL gives us an access token,
                    // with no user interaction required. Eg, when prior approval
                    // has already been granted.
                    try
                    {
                        if (r["oauth_token_secret"] != null)
                            this["token_secret"] = r["oauth_token_secret"];
                    }
                    catch { }
                    return r;
                }
            }
        }

        public OAuthResponse AcquireAccessToken(string uri, string method, string pin)
        {
            NewRequest();
            _params["verifier"] = pin;
            var authzHeader = GetAuthorizationHeader(uri, method);

            // prepare the token request
            var request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(uri);
            request.Headers.Add("Authorization", authzHeader);
            request.Method = method;
            try
            {
                using (var response = (System.Net.HttpWebResponse)request.GetResponse())
                {
                    using (var reader = new System.IO.StreamReader(response.GetResponseStream()))
                    {
                        var r = new OAuthResponse(reader.ReadToEnd());
                        this["token"] = r["oauth_token"];
                        this["token_secret"] = r["oauth_token_secret"];
                        return r;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.ReadLine();
                return null;
            }
        }

        public string GetAuthorizationHeader(string uri, string method)
        {
            return GetAuthorizationHeader(uri, method, null);
        }

        public string GetAuthorizationHeader(string uri, string method, string realm)
        {
            if (string.IsNullOrEmpty(this._params["consumer_key"]))
                throw new ArgumentNullException("consumer_key");

            if (string.IsNullOrEmpty(this._params["signature_method"]))
                throw new ArgumentNullException("signature_method");

            Sign(uri, method);

            var erp = EncodeRequestParameters(this._params);
            return (String.IsNullOrEmpty(realm))
                ? "OAuth " + erp
                : String.Format("OAuth realm=\"{0}\", ", realm) + erp;
        }

        public void Sign(string uri, string method)
        {
            var signatureBase = GetSignatureBase(uri, method);
            var hash = GetHash();

            byte[] dataBuffer = System.Text.Encoding.ASCII.GetBytes(signatureBase);
            byte[] hashBytes = hash.ComputeHash(dataBuffer);

            this["signature"] = Convert.ToBase64String(hashBytes);
        }

        public string GetSignatureBase(string url, string method)
        {
            // normalize the URI
            var uri = new Uri(url);
            var normUrl = string.Format("{0}://{1}", uri.Scheme, uri.Host);
            if (!((uri.Scheme == "http" && uri.Port == 80) ||
                  (uri.Scheme == "https" && uri.Port == 443)))
                normUrl += ":" + uri.Port;

            normUrl += uri.AbsolutePath;

            // the sigbase starts with the method and the encoded URI
            var sb = new System.Text.StringBuilder();
            sb.Append(method)
                .Append('&')
                .Append(UrlEncode(normUrl))
                .Append('&');

            // the parameters follow - all oauth params plus any params on
            // the uri
            // each uri may have a distinct set of query params
            var p = ExtractQueryParameters(uri.Query);
            // add all non-empty params to the "current" params
            foreach (var p1 in this._params)
            {
                // Exclude all oauth params that are secret or
                // signatures; any secrets should be kept to ourselves,
                // and any existing signature will be invalid.

                if (!String.IsNullOrEmpty(this._params[p1.Key]) &&
                    !p1.Key.EndsWith("secret")
                    && !p1.Key.EndsWith("signature")
                    && !p1.Key.EndsWith("accountId") && !p1.Key.EndsWith("clientOrderId")
                    && !p1.Key.EndsWith("limitPrice") && !p1.Key.EndsWith("quantity")
                    && !p1.Key.EndsWith("symbol") && !p1.Key.EndsWith("callOrPut")
                    && !p1.Key.EndsWith("strikePrice") && !p1.Key.EndsWith("expirationYear")
                    && !p1.Key.EndsWith("expirationMonth") && !p1.Key.EndsWith("expirationDay")
                    && !p1.Key.EndsWith("orderAction") && !p1.Key.EndsWith("priceType")
                    && !p1.Key.EndsWith("orderTerm") && !p1.Key.EndsWith("allOrNone")
                    && !p1.Key.EndsWith("routingDestination"))
                    p.Add("oauth_" + p1.Key, p1.Value);
                if (!String.IsNullOrEmpty(p1.Value) && !p1.Key.EndsWith("secret") && !p1.Key.EndsWith("token")
                       && (p1.Key.EndsWith("accountId") || p1.Key.EndsWith("clientOrderId")
                       || p1.Key.EndsWith("limitPrice") || p1.Key.EndsWith("quantity")
                       || p1.Key.EndsWith("symbol") || p1.Key.EndsWith("callOrPut")
                       || p1.Key.EndsWith("strikePrice") || p1.Key.EndsWith("expirationYear")
                       || p1.Key.EndsWith("expirationMonth") || p1.Key.EndsWith("expirationDay")
                       || p1.Key.EndsWith("orderAction") || p1.Key.EndsWith("priceType")
                       || p1.Key.EndsWith("orderTerm") || p1.Key.EndsWith("allOrNone")
                       || p1.Key.EndsWith("routingDestination")))
                {
                    p.Add(p1.Key, p1.Value);
                }

            }

            // concat+format all those params
            var sb1 = new System.Text.StringBuilder();
            foreach (KeyValuePair<String, String> item in p.OrderBy(x => x.Key))
            {
                // even "empty" params need to be encoded this way.
                sb1.AppendFormat("{0}={1}&", item.Key, item.Value);
            }

            // append the UrlEncoded version of that string to the sigbase
            sb.Append(UrlEncode(sb1.ToString().TrimEnd('&')));
            var result = sb.ToString();
            return result;
        }

        public HashAlgorithm GetHash()
        {
            if (this["signature_method"] != "HMAC-SHA1")
                throw new NotImplementedException();

            string keystring = string.Format("{0}&{1}",
                                             UrlEncode(this["consumer_secret"]),
                                             this["token_secret"]);
            var hmacsha1 = new HMACSHA1
            {
                Key = System.Text.Encoding.ASCII.GetBytes(keystring)
            };
            return hmacsha1;
        }

        public static readonly DateTime _epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0);
        public Dictionary<String, String> _params;
        public Random _random;
    }

    public class OAuthResponse
    {

        public string AllText { get; set; }
        public Dictionary<String, String> _params;

        public string this[string ix]
        {
            get
            {
                return _params[ix];
            }
        }

        public OAuthResponse(string alltext)
        {
            AllText = alltext;
            _params = new Dictionary<String, String>();
            var kvpairs = alltext.Split('&');
            foreach (var pair in kvpairs)
            {
                var kv = pair.Split('=');
                _params.Add(kv[0], kv[1]);
            }
        }
    }

}
