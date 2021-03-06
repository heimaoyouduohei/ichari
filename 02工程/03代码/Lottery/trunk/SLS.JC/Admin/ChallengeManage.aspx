﻿<%@ Page Language="C#" AutoEventWireup="true" CodeFile="ChallengeManage.aspx.cs" Inherits="Admin_ChallengeManage" %>

<!DOCTYPE html PUBLIC "-//W3C//DTD XHTML 1.0 Transitional//EN" "http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd">
<%@ Register Assembly="Shove.Web.UI.4 For.NET 3.5" Namespace="Shove.Web.UI" TagPrefix="ShoveWebUI" %>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>无标题页</title>
    <link href="../Style/Admin.css" type="text/css" rel="stylesheet" />
    
    <script language="javascript" type="text/javascript" src="../Components/My97DatePicker/WdatePicker.js"></script>
</head>
<body>
    <form id="form1" runat="server">
    <div>
    <div>
        <br />
        <table width="96%" height="34" border="0" align="center" cellpadding="0" cellspacing="1"
            bgcolor="#85BDE2">
            <tr>
                <td height="32" bgcolor="#B0D5EC" class="STYLE14">
                    <div align="left" class="STYLE4">
                        <div align="center">
                            擂台信息一览表</div>
                    </div>
                </td>
            </tr>
        </table>
        <table width="96%" border="0" align="center" cellpadding="0" cellspacing="0">
            <tr>
                <td align="left" style="height: 70px">
                &nbsp;&nbsp;&nbsp;&nbsp;&nbsp;&nbsp;
                    用户名：<asp:TextBox ID="tbUserName" runat="server"></asp:TextBox>
                    <ShoveWebUI:ShoveConfirmButton ID="btnSearch" BackgroupImage="../Images/Admin/buttbg.gif" runat="server" BorderWidth="0px" Width="60px" Text="搜索用户" BorderStyle="None" Height="20px" OnClick="btnSearch_Click" />&nbsp;
                    开始日期
                    <asp:TextBox runat="server" ID="tbBeginTime" Width="100px" name="tbBeginTime" onFocus="WdatePicker({el:'tbBeginTime',dateFmt:'yyyy-MM-dd', maxDate:'%y-%M-%d'})"
                        Height="15px" />
                    &nbsp;结束日期
                    <asp:TextBox runat="server" ID="tbEndTime" Width="100px" name="tbEndTime" onFocus="WdatePicker({el:'tbEndTime',dateFmt:'yyyy-MM-dd', maxDate:'%y-%M-%d'})"
                        Height="15px" />
                    <ShoveWebUI:ShoveConfirmButton ID="ShoveConfirmButton1" BackgroupImage="../Images/Admin/buttbg.gif"
                        runat="server" BorderWidth="0px" Width="60px" Text="搜索" BorderStyle="None" Height="20px"
                        OnClick="btnSearchByRegDate_Click" />&nbsp
                    
                </td>
            </tr>
            <tr>
                <td align="center">
                    <asp:DataGrid ID="gRanking" runat="server" CellPadding="0" BackColor="White" BorderWidth="2px"
                        BorderStyle="None" BorderColor="#E0E0E0" Font-Names="宋体" PageSize="20" AllowSorting="True"
                        AutoGenerateColumns="False" AllowPaging="True" Width="100%">
                        <FooterStyle ForeColor="#330099" BackColor="#FFFFCC"></FooterStyle>
                        <SelectedItemStyle Font-Bold="True" ForeColor="#663399"></SelectedItemStyle>
                        <HeaderStyle HorizontalAlign="Center" ForeColor="RoyalBlue" VerticalAlign="Middle"
                            BackColor="LightGray" Wrap="True" Height="16px"></HeaderStyle>
                        <Columns>
                            <asp:BoundColumn DataField="Ranking" SortExpression="Ranking" HeaderText="排名">
                                <HeaderStyle HorizontalAlign="Center" Font-Bold="False" Font-Italic="False" Font-Overline="False"
                                    Font-Strikeout="False" Font-Underline="False" Wrap="False"></HeaderStyle>
                                <ItemStyle Font-Bold="False" Font-Italic="False" Font-Overline="False" Font-Strikeout="False"
                                    Font-Underline="False" Wrap="False" />
                            </asp:BoundColumn>
                            <asp:BoundColumn DataField="Name" HeaderText="用户名">
                                <HeaderStyle HorizontalAlign="Center" Font-Bold="False" Font-Italic="False" Font-Overline="False"
                                    Font-Strikeout="False" Font-Underline="False" Wrap="False"></HeaderStyle>
                                <ItemStyle Font-Bold="False" Font-Italic="False" Font-Overline="False" Font-Strikeout="False"
                                    Font-Underline="False" Wrap="False" />
                            </asp:BoundColumn>
                            <asp:BoundColumn DataField="BetCount" SortExpression="BetCount" HeaderText="投注次数">
                                <HeaderStyle HorizontalAlign="Center" Font-Bold="False" Font-Italic="False" Font-Overline="False"
                                    Font-Strikeout="False" Font-Underline="False" Wrap="False"></HeaderStyle>
                                <ItemStyle Font-Bold="False" Font-Italic="False" Font-Overline="False" Font-Strikeout="False"
                                    Font-Underline="False" Wrap="False" />
                            </asp:BoundColumn>
                            <asp:BoundColumn DataField="WinCount" SortExpression="WinCount" HeaderText="命中次数">
                                <HeaderStyle HorizontalAlign="Center" Font-Bold="False" Font-Italic="False" Font-Overline="False"
                                    Font-Strikeout="False" Font-Underline="False" Wrap="False"></HeaderStyle>
                                <ItemStyle Font-Bold="False" Font-Italic="False" Font-Overline="False" Font-Strikeout="False"
                                    Font-Underline="False" Wrap="False" />
                            </asp:BoundColumn>
                            <asp:BoundColumn DataField="sumMoney" SortExpression="sumMoney" HeaderText="中奖次数">
                                <HeaderStyle HorizontalAlign="Center" Font-Bold="False" Font-Italic="False" Font-Overline="False"
                                    Font-Strikeout="False" Font-Underline="False" Wrap="False"></HeaderStyle>
                                <ItemStyle Font-Bold="False" Font-Italic="False" Font-Overline="False" Font-Strikeout="False"
                                    Font-Underline="False" Wrap="False" />
                            </asp:BoundColumn>
                            <asp:BoundColumn DataField="Score" SortExpression="Score" HeaderText="总积分">
                                <HeaderStyle HorizontalAlign="Center" Font-Bold="False" Font-Italic="False" Font-Overline="False"
                                    Font-Strikeout="False" Font-Underline="False" Wrap="False"></HeaderStyle>
                                <ItemStyle Font-Bold="False" Font-Italic="False" Font-Overline="False" Font-Strikeout="False"
                                    Font-Underline="False" Wrap="False" />
                            </asp:BoundColumn>
                            <asp:BoundColumn DataField="TotalWinMoney" SortExpression="TotalWinMoney" HeaderText="总奖金">
                                <HeaderStyle HorizontalAlign="Center" Font-Bold="False" Font-Italic="False" Font-Overline="False"
                                    Font-Strikeout="False" Font-Underline="False" Wrap="False"></HeaderStyle>
                                <ItemStyle Font-Bold="False" Font-Italic="False" Font-Overline="False" Font-Strikeout="False"
                                    Font-Underline="False" Wrap="False" />
                            </asp:BoundColumn>
                           
                        </Columns>
                        <PagerStyle Visible="False" HorizontalAlign="Center" ForeColor="#330099" BackColor="#FFFFCC">
                        </PagerStyle>
                        <ItemStyle Wrap="True" HorizontalAlign="Center" VerticalAlign="Middle" />
                    </asp:DataGrid>
                    <ShoveWebUI:ShoveGridPager ID="gPager" PageSize="20" runat="server" Width="100%"
                        ShowSelectColumn="False" DataGrid="gRanking" AlternatingRowColor="Linen" GridColor="#E0E0E0"
                        HotColor="MistyRose" Visible="False" OnPageWillChange="gPager_PageWillChange"
                        OnSortBefore="gPager_SortBefore"></ShoveWebUI:ShoveGridPager>
                        
                </td>
            </tr>
        </table>
        <br />
    </div>
    </div>
    </form>
</body>
</html>
