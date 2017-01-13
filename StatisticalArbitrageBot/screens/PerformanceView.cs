using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Data.SqlClient;

namespace StatisticalArbitrageBot
{
    public partial class PerformanceView : Form
    {
        public SqlConnection thisconnect;
        public PerformanceView()
        {
            InitializeComponent();
        }
    }
}
