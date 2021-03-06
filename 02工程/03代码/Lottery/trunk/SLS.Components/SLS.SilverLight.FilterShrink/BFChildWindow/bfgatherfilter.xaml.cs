﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;
using SLS.SilverLight.FilterShrink.Model;

namespace SLS.SilverLight.FilterShrink.BFChildWindow
{
    public partial class bfgatherfilter : ChildWindow
    {
        public StackPanel StackPanel;

        public List<List<string>> SelectedRate;//父窗体传过来的已经选择的按钮

        public string ListStr = string.Empty;//父窗体传来的过滤条件

        DispatcherTimer Timer;

        public bfgatherfilter()
        {
            InitializeComponent();
        }

        //替换
        void btnShift_Click(object sender, RoutedEventArgs e)
        {
            //throw new NotImplementedException();
            int Index = lbResult.SelectedIndex;
            if (Index == -1) return;

            string Str = string.Empty;
            string RowsStr = string.Empty;

            var selfcols = dgMatchInfo.Columns[0];

            foreach (var item in dgMatchInfo.ItemsSource)
            {
                Str = "";
                //--对象所在的单元格
                var cells = selfcols.GetCellContent(item);
                if (cells != null)
                {
                    //--单元格所包含的元素
                    Grid Grid = cells as Grid;

                    StackPanel Sp = Grid.Children[0] as StackPanel;

                    StackPanel Sp1 = Sp.Children[1] as StackPanel;

                    foreach (StackPanel Sp2 in Sp1.Children)
                    {
                        foreach (Button b in Sp2.Children)
                        {
                            SolidColorBrush Brush = (SolidColorBrush)(b.Background);
                            if (Brush.Color.ToString() == "#FFFF0000")
                            {
                                Str += b.Content.ToString();
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(Str))
                {
                    Str = "#";
                }

                RowsStr += Str + ",";
            }

            RowsStr = RowsStr.Substring(0, RowsStr.Length - 1);

            string Result = RowsStr + "|" + this.cbStart.SelectedItem.ToString() + "-" + this.cbEnd.SelectedItem.ToString();

            lbResult.Items.RemoveAt(Index);
            lbResult.Items.Insert(Index, Result);
        }

        //删除单个
        void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            //throw new NotImplementedException();
            int Index = lbResult.SelectedIndex;
            if (Index == -1) return;
            lbResult.Items.RemoveAt(Index);
        }

        //全部删除
        void btnAllDel_Click(object sender, RoutedEventArgs e)
        {
            //throw new NotImplementedException();
            lbResult.Items.Clear();
        }

        //添加按钮
        void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            string Str = string.Empty;
            string RowsStr = string.Empty;

            var selfcols = dgMatchInfo.Columns[0];

            foreach (var item in dgMatchInfo.ItemsSource)
            {
                Str = "";
                //--对象所在的单元格
                var cells = selfcols.GetCellContent(item);
                if (cells != null)
                {
                    //--单元格所包含的元素
                    Grid Grid = cells as Grid;

                    StackPanel Sp = Grid.Children[0] as StackPanel;

                    StackPanel Sp1 = Sp.Children[1] as StackPanel;

                    foreach (StackPanel Sp2 in Sp1.Children)
                    {
                        foreach (Button b in Sp2.Children)
                        {
                            SolidColorBrush Brush = (SolidColorBrush)(b.Background);
                            if (Brush.Color.ToString() == "#FFFF0000")
                            {
                                Str += b.Content.ToString();
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(Str))
                {
                    Str = "#";
                }

                RowsStr += Str + ",";
            }

            RowsStr = RowsStr.Substring(0, RowsStr.Length - 1);

            string Result = RowsStr + "|" + this.cbStart.SelectedItem.ToString() + "-" + this.cbEnd.SelectedItem.ToString();
            lbResult.Items.Add(Result);
        }

        //确定按钮
        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            string Str = string.Empty;

            if (lbResult.Items.Count > 0)
            {
                foreach (string s in lbResult.Items)
                {
                    Str += s + ";";
                }
                Str = Str.Substring(0, Str.Length - 1);
                HidResult.Text = Str;
            }
            else
            {
                MessageBox.Show("没有任何数据！");
                return;
            }
            this.DialogResult = true;
        }

        //取消按钮
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }

        //打开该窗体后，加载datagrid
        public void BindDataGrid(List<Netball> NetBallList, StackPanel sp)
        {
            cbStart.Items.Clear();
            cbEnd.Items.Clear();

            dgMatchInfo.ItemsSource = NetBallList;
            //DataGrid3.ItemsSource = NetBallList;

            StackPanel = sp;

            for (int i = 0; i <= NetBallList.Count; i++)
            {
                cbStart.Items.Add(i.ToString());
                cbEnd.Items.Add(i.ToString());
            }
            cbStart.SelectedIndex = 0;
            cbEnd.SelectedIndex = 0;

            switch (this.Title.ToString())
            {
                case "命中场次":
                    tbName.Text = "<=命中场次范围<=";
                    break;
                case "冷门过滤":
                    tbName.Text = "<=冷门命中范围<=";
                    break;
                case "叠加过滤":
                    tbName.Text = "<=复式命中范围<=";
                    break;
            }

            //延时初始化指数和、指数积、奖金
            Timer = new DispatcherTimer();
            Timer.Interval = new TimeSpan(0, 0, 0, 0, 200);
            Timer.Tick += new EventHandler(Timer_Tick);
            Timer.Start();
        }

        void Timer_Tick(object sender, EventArgs e)
        {
            var selfcols = this.dgMatchInfo.Columns[0];

            string First = string.Empty;

            foreach (var item in dgMatchInfo.ItemsSource)
            {
                //--对象所在的单元格
                var cells = selfcols.GetCellContent(item);

                if (cells != null)
                {
                    //--单元格所包含的元素
                    Grid grid = cells as Grid;

                    StackPanel Sp = grid.Children[0] as StackPanel;

                    StackPanel Sp1 = Sp.Children[1] as StackPanel;

                    foreach (StackPanel Sp2 in Sp1.Children)
                    {
                        foreach (Button b in Sp2.Children)
                        {
                            b.Background = new SolidColorBrush(Colors.White);
                            b.IsEnabled = true;
                        }
                    }
                }
            }

            lbResult.Items.Clear();

            foreach (string s in ListStr.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                lbResult.Items.Add(s);
            }

            string Title = this.Title.ToString();
            if (Title == "冷门过滤")
            {
                int i = 0;
                foreach (var item in dgMatchInfo.ItemsSource)
                {
                    //--对象所在的单元格
                    var cells = selfcols.GetCellContent(item);

                    if (cells != null)
                    {
                        //--单元格所包含的元素
                        Grid grid = cells as Grid;

                        StackPanel Sp = grid.Children[0] as StackPanel;

                        StackPanel Sp1 = Sp.Children[1] as StackPanel;

                        foreach (StackPanel Sp2 in Sp1.Children)
                        {
                            foreach (Button b in Sp2.Children)
                            {
                                b.IsEnabled = SelectedRate[i].Contains(b.Content.ToString());
                            }
                        }
                        i++;
                    }

                }
            }

            Timer.Stop();
        }

        private void Button_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // TODO: Add event handler implementation here.

            Button button = sender as Button;
            SolidColorBrush brush = (SolidColorBrush)(button.Background);

            if (brush.Color.ToString() == "#FFFF0000")
            {
                button.Background = new SolidColorBrush(Colors.White);
            }
            else
            {
                button.Background = new SolidColorBrush(Colors.Red);
            }
        }
    }
}
