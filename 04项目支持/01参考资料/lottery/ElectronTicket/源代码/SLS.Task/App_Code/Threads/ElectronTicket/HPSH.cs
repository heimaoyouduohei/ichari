﻿using System;
using System.Data;
using System.Configuration;
using System.Net;
using System.IO;
using System.Xml;
using System.Xml.XPath;
using System.Collections;
using System.Text;

using System.Linq;

using Shove.Database;
using SLS.Task.Helper;

namespace SLS.Task
{
    /// <summary>
    /// HPSH 的摘要说明
    /// </summary>
    public class ElectronTicket_HPSH
    {
        private const int TimeoutSeconds = 90;

        private long gCount1 = 0;
        private long gCount2 = 0;
        private long gCount3 = 0;

        private System.Threading.Thread thread;
        private string ConnectionString;

        private Message msg = new Message("ElectronTicket_HPSH");
        private Log log = new Log("ElectronTicket_HPSH");

        private SMS.Eucp.Gateway.Gateway segg = null;

        public int StateService = 0;   // 0 停止 1 运行中 2 置为停止

        public string ElectronTicket_HPSH_UserName;
        public string ElectronTicket_HPSH_UserPassword;
        public string ElectronTicket_HPSH_Getway;

        public string ElectronTicket_PrintOut_AlipayName;
        public string ElectronTicket_PrintOut_IDCardNumber;
        public string ElectronTicket_PrintOut_Mobile;
        public string ElectronTicket_PrintOut_RealityName;
        public string ElectronTicket_PrintOut_Email;

        public ElectronTicket_HPSH(string connectionstring)
        {
            ConnectionString = connectionstring;
        }

        public void Run()
        {
            SystemOptions so = new SystemOptions(ConnectionString);

            if (!so["ElectronTicket_HPSH_Status_ON"].ToBoolean(false))
            {
                return;
            }

            if ((ElectronTicket_HPSH_Getway == "") || (ElectronTicket_HPSH_UserName == "") || (ElectronTicket_HPSH_UserPassword == ""))
            {
                msg.Send("ElectronTicket_HPSH Task 参数配置不完整.");
                log.Write("ElectronTicket_HPSH Task 参数配置不完整.");

                return;
            }

            // 已经启动
            if (StateService == 1)
            {
                return;
            }

            lock (this) // 确保临界区被一个 Thread 所占用
            {
                StateService = 1;

                gCount1 = 0;
                gCount2 = 0;
                gCount3 = 0;

                thread = new System.Threading.Thread(new System.Threading.ThreadStart(Do));
                thread.IsBackground = true;

                thread.Start();

                msg.Send("ElectronTicket_HPSH Task Start."); 
                log.Write("ElectronTicket_HPSH Task Start.");
            }
        }

        public void Exit()
        {
            StateService = 2;
        }

        public void Do()
        {
            while (true)
            {
                if (StateService == 2)
                {
                    msg.Send("ElectronTicket_HPSH Task Stop.");
                    log.Write("ElectronTicket_HPSH Task Stop.");

                    StateService = 0;

                    Stop();

                    return;
                }

                System.Threading.Thread.Sleep(1000);   // 1秒为单位

                gCount1++;
                gCount2++;
                gCount3++;

                #region 10 秒，发送电子票数据

                if (gCount1 >= 10 * 1)
                {
                    gCount1 = 0;

                    try
                    {
                        WriteTickets();             // 满员方案拆分为票

                        msg.Send("WriteTickets ...... OK.");
                    }
                    catch (Exception e)
                    {
                        msg.Send("WriteTickets is Fail: " + e.Message);
                        log.Write("WriteTickets is Fail: " + e.Message);
                    }

                    try
                    {
                        QueryTickets();             // 代购票查询

                        msg.Send("QueryTickets ...... OK.");
                    }
                    catch (Exception e)
                    {
                        msg.Send("QueryTickets is Fail: " + e.Message);
                        log.Write("QueryTickets is Fail: " + e.Message);
                    }

                    try
                    {
                        SendTickets();              // 发送代购电子票

                        msg.Send("SendTickets ...... OK.");
                    }
                    catch (Exception e)
                    {
                        msg.Send("SendTickets is Fail: " + e.Message);
                        log.Write("SendTickets is Fail: " + e.Message);
                    }
                }

                #endregion

                #region 2 分钟，查询奖期状态

                if (gCount2 >= 60 * 1)
                {
                    gCount2 = 0;

                    try
                    {
                        QueryIsuseState();      // 查询奖期状态

                        msg.Send("QueryIsuseState ...... OK.");
                    }
                    catch (Exception e)
                    {
                        msg.Send("QueryIsuseState is Fail: " + e.Message);
                        log.Write("QueryIsuseState is Fail: " + e.Message);
                    }
                }

                #endregion

                #region 20 分钟，开奖查询

                if (gCount3 >= 60 * 20)
                {
                    gCount3 = 0;

                    try
                    {
                        QueryIsuseOpen();      // 查询奖期开奖结果

                        msg.Send("QueryIsuseOpen ...... OK.");
                    }
                    catch (Exception e)
                    {
                        msg.Send("QueryIsuseOpen is Fail: " + e.Message);
                        log.Write("QueryIsuseOpen is Fail: " + e.Message);
                    }
                }

                #endregion
            }
        }

        private void Stop()
        {
            if (thread != null)
            {
                thread.Abort();
                thread = null;
            }
        }

        #region 定时器执行的事件

        // 满员方案拆分为票
        private void WriteTickets()
        {
            DataTable dt = new DAL.Views.V_SchemeSchedules().Open(ConnectionString, "ID, LotteryID, PlayTypeID, LotteryNumber, Multiple, Money, (case LotteryID when 29 then -29 else LotteryID end) as LotteryID_2", "Buyed = 0 and (GetDate() between StartTime and EndTime) and BuyedShare >= Share and isnull(Identifiers, '') = '' and PrintOutType = 102 and State = 1 and dateadd(mi, 1, StateUpdateTime) <= GetDate() and LotteryID <> 29", "LotteryID_2, UserType desc, [ID]");

            if (dt == null)
            {
                msg.Send("读取方案错误(WriteTickets)。");
                log.Write("读取方案错误(WriteTickets)。");

                return;
            }

            DAL.Tables.T_Schemes t_Schemes = new DAL.Tables.T_Schemes();

            foreach (DataRow dr in dt.Rows)
            {
                long SchemeID = Shove._Convert.StrToLong(dr["ID"].ToString(), -1);
                int LotteryID = Shove._Convert.StrToInt(dr["LotteryID"].ToString(), -1);
                string LotterNumber = dr["LotteryNumber"].ToString();
                int PlayTypeID = Shove._Convert.StrToInt(dr["PlayTypeID"].ToString(), -1);
                int Multiple = Shove._Convert.StrToInt(dr["Multiple"].ToString(), -1);

                if ((SchemeID < 0) || (LotteryID < 0) || (PlayTypeID < 0) || (Multiple < 1))
                {
                    msg.Send("读取方案错误(WriteTickets)。方案号：" + SchemeID.ToString());
                    log.Write("读取方案错误(WriteTickets)。方案号：" + SchemeID.ToString());

                    continue;
                }

                double Money = 0;
                SLS.Lottery.Ticket[] Tickets = null;

                try
                {
                    Tickets = new SLS.Lottery()[LotteryID].ToElectronicTicket_HPSH(PlayTypeID, LotterNumber, Multiple, 200, ref Money);
                }
                catch(Exception e)
                {
                    msg.Send("拆票错误(WriteTickets)。方案号：" + SchemeID.ToString() + "，" + e.Message);
                    log.Write("拆票错误(WriteTickets)。方案号：" + SchemeID.ToString() + "，" + e.Message);

                    continue;
                }

                if (Tickets == null)
                {
                    msg.Send("分解票错误(WriteTickets)。方案号：" + SchemeID.ToString());
                    log.Write("分解票错误(WriteTickets)。方案号：" + SchemeID.ToString());

                    continue;
                }

                if (Money != Shove._Convert.StrToDouble(dr["Money"].ToString(), -1))
                {
                    msg.Send("异常警告！！！！(WriteTickets)。方案号： " + SchemeID.ToString() + " 的购买金额与实际票的金额不符合！！！！");
                    log.Write("异常警告！！！！(WriteTickets)。方案号： " + SchemeID.ToString() + " 的购买金额与实际票的金额不符合！！！！");

                    SMS.Eucp.Gateway.CallResult SmsResult = segg.Send("13537697101", "异常警告！！！！(WriteTickets)。方案号： " + SchemeID.ToString() + " 的购买金额与实际票的金额不符合！！！！");

                    if (SmsResult.Code < 0)
                    {
                        log.Write("Send is Fail: " + SmsResult.Description);
                    }

                    SmsResult = segg.Send("13612833534", "异常警告！！！！(WriteTickets)。方案号： " + SchemeID.ToString() + " 的购买金额与实际票的金额不符合！！！！");

                    continue;
                }

                int TicketPlayTypeID = Tickets[0].PlayTypeID;

                string TicketXML = "<Tickets>";

                foreach (SLS.Lottery.Ticket ticket in Tickets)
                {
                    TicketXML += "<Ticket LotteryNumber=\"" + ticket.Number + "\" Multiple=\"" + ticket.Multiple + "\" Money=\"" + ticket.Money + "\" />";
                }

                TicketXML += "</Tickets>";

                int ReturnValue = 0;
                string ReturnDescription = "";

                int Result = DAL.Procedures.P_SchemesSendToCenterAdd(ConnectionString, SchemeID, TicketPlayTypeID, TicketXML, ref ReturnValue, ref ReturnDescription);

                if ((Result < 0) || (ReturnValue < 0))
                {
                    msg.Send("票写入错误(WriteTickets)：方案号：" + SchemeID.ToString() + "，" + ReturnDescription);
                    log.Write("票写入错误(WriteTickets)：方案号：" + SchemeID.ToString() + "，" + ReturnDescription);
                }
            }
        }

        // 代购票查询
        private void QueryTickets()
        {
            DataTable dt = new DAL.Views.V_SchemesSendToCenter().Open(ConnectionString, "distinct SchemeID", "(((Sends > 0) AND (Sends < 100)) or (sends = 3301) or (sends = 2148)) AND (HandleResult = 0) AND (IsOpened = 0) and LotteryID <> 29  and PrintOutType = 102", "");

            if (dt == null)
            {
                msg.Send("查询代购票出错(QueryTickets)：读取未成功票错误。");
                log.Write("查询代购票出错(QueryTickets)：读取未成功票错误。");

                return;
            }

            if (dt.Rows.Count < 1)
            {
                return;
            }

            DataTable dtSchemesSendToCenter = null;

            DAL.Tables.T_SchemesSendToCenter t_SchemesSendToCenter = new DAL.Tables.T_SchemesSendToCenter();

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                dtSchemesSendToCenter = new DAL.Tables.T_SchemesSendToCenter().Open(ConnectionString, "top 1 *", "schemeid=" + dt.Rows[i]["SchemeID"].ToString() + " and (Sends > 0) AND (Sends < 100)", "");

                if (dtSchemesSendToCenter == null)
                {
                    continue;
                }

                if (dtSchemesSendToCenter.Rows.Count < 1)
                {
                    continue;
                }

                System.Threading.Thread.Sleep(1000);

                string ticketid = dtSchemesSendToCenter.Rows[0]["Identifiers"].ToString();
                string Body = "<body><ticketQuery>" + "<ticket id=\"" + ticketid + "\"/>" + "</ticketQuery></body>";

                string MessageID = ElectronTicket_HPSH_UserName + DateTime.Now.ToString("yyyyMMdd") + DateTime.Now.ToString("HHmmss") + (i % 100).ToString().PadLeft(2, '0');
                string TimeStamp = DateTime.Now.ToString("yyyyMMdd") + DateTime.Now.ToString("HHmmss");

                string Message = "<?xml version=\"1.0\" encoding=\"GBK\"?>";
                Message += "<message version=\"1.0\" id=\"" + MessageID + "\">";
                Message += "<header>";
                Message += "<messengerID>" + ElectronTicket_HPSH_UserName + "</messengerID>";
                Message += "<timestamp>" + TimeStamp + "</timestamp>";
                Message += "<transactionType>105</transactionType>";
                Message += "<digest>" + Shove._Security.Encrypt.MD5(MessageID + TimeStamp + ElectronTicket_HPSH_UserPassword + Body, "gb2312") + "</digest>";
                Message += "</header>";
                Message += Body;
                Message += "</message>";

                WriteElectronTicketLog(true, "105", "transType=105&transMessage=" + Message);

                string ReceiveString = "";

                try
                {
                    ReceiveString = PublicFunction.Post(ElectronTicket_HPSH_Getway, "transType=105&transMessage=" + Message, TimeoutSeconds);
                }
                catch
                {
                    continue;
                }

                if (ReceiveString.Length <= 10)
                {
                    continue;
                }

                string[] str_s = ReceiveString.Split('&');

                if ((str_s == null) || (str_s.Length < 1))
                {
                    continue;
                }

                string TransType = str_s[0];
                string TransMessage = str_s[1];

                TransType = TransType.Substring(10);
                TransMessage = TransMessage.Substring(13);

                WriteElectronTicketLog(false, TransType, ReceiveString);

                System.Xml.XmlDocument XmlDoc = new XmlDocument();
                System.Xml.XmlNodeList nodes = null;

                try
                {
                    XmlDoc.Load(new StringReader(TransMessage));
                    nodes = XmlDoc.GetElementsByTagName("ticket");
                }
                catch { }

                if (nodes == null)
                {
                    continue;
                }

                for (int k = 0; k < nodes.Count; k++)
                {
                    string Identifiers = nodes[k].Attributes["id"].Value;
                    string Status = nodes[k].Attributes["status"].Value;
                    string _Message = nodes[k].Attributes["message"].Value;

                    if (Status == "0000")
                    {
                        string DealTime = nodes[k].Attributes["dealTime"].Value;

                        int ReturnValue = 0;
                        string ReturnDescription = "";

                        int Result = DAL.Procedures.P_SchemesSendToCenterHandleUniteAnte(ConnectionString, Shove._Convert.StrToLong(dt.Rows[i]["SchemeID"].ToString(), -1), DateTime.Now, true, ref ReturnValue, ref ReturnDescription);

                        if ((Result < 0) || (ReturnValue < 0))
                        {
                            msg.Send("对所查询到的电子票数据第一次处理出错(QueryTickets)：数据读写错误。票号：" + Identifiers + "，" + ReturnDescription);
                            log.Write("对所查询到的电子票数据第一次处理出错(QueryTickets)：数据读写错误。票号：" + Identifiers + "，" + ReturnDescription);

                            System.Threading.Thread.Sleep(1000);

                            ReturnValue = 0;
                            ReturnDescription = "";

                            Result = DAL.Procedures.P_SchemesSendToCenterHandleUniteAnte(ConnectionString, Shove._Convert.StrToLong(dt.Rows[i]["SchemeID"].ToString(), -1), Shove._Convert.StrToDateTime(DealTime, DateTime.Now.ToString()), true, ref ReturnValue, ref ReturnDescription);

                            if ((Result < 0) || (ReturnValue < 0))
                            {
                                msg.Send("对所查询到的电子票数据第二次处理出错(QueryTickets)：数据读写错误。票号：" + Identifiers + "，" + ReturnDescription);
                                log.Write("对所查询到的电子票数据第二次处理出错(QueryTickets)：数据读写错误。票号：" + Identifiers + "，" + ReturnDescription);
                            }
                        }

                        continue;
                    }

                    if ("0010 0011 0014 0015 0016 0098 0097 1008 1009 1010 1012 1016 1017 2001 2002 2003 2004 2010 2011 2012 2013 2014 2015 2016 2017 2018 2030 2031 2040 2041 2042 -1 2043 2044 2046 3000 3002 3004 3005 3011 3012 3013 3014 3015 3016 3017 3018 3100 3101 3202 3203 3204 3205".IndexOf(Status) >= 0)
                    {
                        t_SchemesSendToCenter.Sends.Value = Status + 100;
                        t_SchemesSendToCenter.Update(ConnectionString, "SchemeID = " + dt.Rows[i]["SchemeID"].ToString());

                        continue;
                    }

                    if (Status == "2052")
                    {
                        System.Threading.Thread.Sleep(1000);

                        continue;
                    }

                    if (Status == "2032")
                    {
                        t_SchemesSendToCenter.Sends.Value = "99";
                        t_SchemesSendToCenter.Update(ConnectionString, "SchemeID = " + dt.Rows[i]["SchemeID"].ToString());

                        continue;
                    }
                }
            }
        }

        // 发送代购电子票
        private void SendTickets()
        {
            DAL.Views.V_SchemesSendToCenter v_SchemesSendToCenter = new DAL.Views.V_SchemesSendToCenter();

            DataTable dt = v_SchemesSendToCenter.Open(ConnectionString, "distinct SchemeID, SiteID, UserType", "Buyed = 0 and (GetDate() between StartTime and EndTime) and Sends < 99 and HandleResult = 0 and State = 1 and LotteryID <> 29 and PrintOutType = 102", " UserType desc");

            if (dt == null)
            {
                msg.Send("发送代购票出错(SendTickets)：读取方案错误。");
                log.Write("发送代购票出错(SendTickets)：读取方案错误。");

                return;
            }

            DAL.Tables.T_SchemesSendToCenter t_SchemesSendToCenter = new DAL.Tables.T_SchemesSendToCenter();

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                if (i % 10 == 0)
                {
                    System.Threading.Thread.Sleep(1000);
                }

                DataTable dtSchemesSend = v_SchemesSendToCenter.Open(ConnectionString, "", "SchemeID=" + dt.Rows[i]["SchemeID"].ToString() + " and Buyed = 0 and (GetDate() between StartTime and EndTime) and Sends < 99 and HandleResult = 0 and State = 1", "");

                if (dtSchemesSend == null)
                {
                    msg.Send("发送代购票出错(SendTickets)：读取方案错误。方案号：" + dt.Rows[i]["SchemeID"].ToString());
                    log.Write("发送代购票出错(SendTickets)：读取方案错误。方案号：" + dt.Rows[i]["SchemeID"].ToString());

                    continue;
                }

                if (dtSchemesSend.Rows.Count < 1)
                {
                    continue;
                }

                string Sends = dtSchemesSend.Rows[0]["Sends"].ToString();

                if (MSSQL.ExecuteNonQuery(ConnectionString, "update T_SchemesSendToCenter set Sends = Sends + 1 where SchemeID = " + dt.Rows[i]["SchemeID"].ToString()) < 0)
                {
                    msg.Send("更新票发送状态时出错(SendTickets)。方案ID：" + dt.Rows[i]["SchemeID"].ToString());
                    log.Write("更新票发送状态时出错(SendTickets)。方案ID：" + dt.Rows[i]["SchemeID"].ToString());

                    continue;
                }

                string ticketid = "";
                string LotteryNumber = "";
                DateTime Now = DateTime.Now;

                string Body = "<body><lotteryRequest>";

                for (int j = 0; j < dtSchemesSend.Rows.Count; j++)
                {
                    DataRow dr = dtSchemesSend.Rows[j];

                    ticketid = dr["Identifiers"].ToString();

                    Body += "<ticket id=\"" + ticketid + "\"";
                    Body += " playType=\"" + dr["PlayTypeID"].ToString() + "\" money=\"" + double.Parse(dr["Money"].ToString()).ToString("N").Replace(",", "") + "\" amount=\"" + dr["Multiple"].ToString() + "\">";
                    Body += "<issue number=\"" + dr["IsuseName"].ToString() + "\" gameName=\"" + GetLotteryName(int.Parse(dr["LotteryID"].ToString())) + "\"/>";
                    Body += "<userProfile userName=\"" + ElectronTicket_PrintOut_AlipayName + "\" cardType=\"1\" mail=\"" + ElectronTicket_PrintOut_Email + "\" cardNumber=\"" + ElectronTicket_PrintOut_IDCardNumber + "\" mobile=\"" + ElectronTicket_PrintOut_Mobile + "\" realName=\"" + ElectronTicket_PrintOut_RealityName + "\" bonusPhone=\"" + ElectronTicket_PrintOut_Mobile + "\"/>";

                    try
                    {
                        LotteryNumber = dr["Ticket"].ToString();
                    }
                    catch
                    {
                        continue;
                    }

                    string[] strs = LotteryNumber.Split('\n');

                    foreach (string str in strs)
                    {
                        if (str.Trim() == "")
                        {
                            continue;
                        }

                        Body += "<anteCode>" + str + "</anteCode>";
                    }

                    Body += "</ticket>";
                }

                Body += "</lotteryRequest></body>";

                string MessageID = ElectronTicket_HPSH_UserName + Now.ToString("yyyyMMdd") + Now.ToString("HHmmss") + (i % 100).ToString().PadLeft(2, '0');
                string TimeStamp = Now.ToString("yyyyMMdd") + Now.ToString("HHmmss");

                string Message = "<?xml version=\"1.0\" encoding=\"GBK\"?>";
                Message += "<message version=\"1.0\" id=\"" + MessageID + "\">";
                Message += "<header>";
                Message += "<messengerID>" + ElectronTicket_HPSH_UserName + "</messengerID>";
                Message += "<timestamp>" + TimeStamp + "</timestamp>";
                Message += "<transactionType>103</transactionType>";
                Message += "<digest>" + Shove._Security.Encrypt.MD5(MessageID + TimeStamp + ElectronTicket_HPSH_UserPassword + Body, "gb2312") + "</digest>";
                Message += "</header>";
                Message += Body;
                Message += "</message>";

                WriteElectronTicketLog(true, "103", "transType=103&transMessage=" + Message);

                string ReceiveString = "";

                try
                {
                    ReceiveString = PublicFunction.Post(ElectronTicket_HPSH_Getway, "transType=103&transMessage=" + Message, TimeoutSeconds);
                }
                catch
                {
                    msg.Send("电子票-103 发送失败(SendTickets)。方案ID：" + dt.Rows[i]["SchemeID"].ToString());
                    log.Write("电子票-103 发送失败(SendTickets)。方案ID：" + dt.Rows[i]["SchemeID"].ToString());

                    continue;
                }

                if (ReceiveString.Length <= 10)
                {
                    continue;
                }

                string[] str_s = ReceiveString.Split('&');

                if ((str_s == null) || (str_s.Length < 1))
                {
                    continue;
                }

                string TransType = str_s[0];
                string TransMessage = str_s[1];

                TransType = TransType.Substring(10);
                TransMessage = TransMessage.Substring(13);

                WriteElectronTicketLog(false, TransType, ReceiveString);

                System.Xml.XmlDocument XmlDoc = new XmlDocument();
                System.Xml.XmlNodeList nodes = null;

                try
                {
                    XmlDoc.Load(new StringReader(TransMessage));
                    nodes = XmlDoc.GetElementsByTagName("*");
                }
                catch
                {
                    continue;
                }

                string code = "";

                for (int j = 0; j < nodes.Count; j++)
                {
                    if (nodes[j].Name.ToUpper() == "RESPONSE")
                    {
                        code = nodes[j].Attributes["code"].Value;
                    }
                }

                int ReturnValue = 0;
                string ReturnDescription = "";

                if (code == "0000")
                {
                    int Result = DAL.Procedures.P_SchemesSendToCenterHandleUniteAnte(ConnectionString, Shove._Convert.StrToLong(dt.Rows[i]["SchemeID"].ToString(), -1), DateTime.Now, true, ref ReturnValue, ref ReturnDescription);

                    if ((Result < 0) || (ReturnValue < 0))
                    {
                        msg.Send("对所发送的成功落地的代购票第一次处理出错(SendTickets)：数据读写错误。票号：" + dt.Rows[i]["SchemeID"].ToString() + "，" + ReturnDescription);
                        log.Write("对所发送的成功落地的代购票第一次处理出错(SendTickets)：数据读写错误。票号：" + dt.Rows[i]["SchemeID"].ToString() + "，" + ReturnDescription);

                        System.Threading.Thread.Sleep(1000);

                        ReturnValue = 0;
                        ReturnDescription = "";

                        Result = DAL.Procedures.P_SchemesSendToCenterHandleUniteAnte(ConnectionString, Shove._Convert.StrToLong(dt.Rows[i]["SchemeID"].ToString(), -1), DateTime.Now, true, ref ReturnValue, ref ReturnDescription);

                        if ((Result < 0) || (ReturnValue < 0))
                        {
                            msg.Send("对所发送的成功落地的代购票第二次处理出错(SendTickets)：数据读写错误。票号：" + dt.Rows[i]["SchemeID"].ToString() + "，" + ReturnDescription);
                            log.Write("对所发送的成功落地的代购票第二次处理出错(SendTickets)：数据读写错误。票号：" + dt.Rows[i]["SchemeID"].ToString() + "，" + ReturnDescription);
                        }
                    }

                    continue;
                }

                int State = 0;
                int SiteID = Shove._Convert.StrToInt(dt.Rows[i]["SiteID"].ToString(), 1);

                try
                {
                    State = int.Parse(code);
                }
                catch { }

                if (code == "2032")     // 限号
                {
                    if (Shove._Convert.StrToInt(Sends, 0) < 99)
                    {
                        t_SchemesSendToCenter.Sends.Value = 99;
                        t_SchemesSendToCenter.Update(ConnectionString, "SchemeID = " + dt.Rows[i]["SchemeID"].ToString());
                    }
                    else
                    {
                        int Result = DAL.Procedures.P_QuashScheme(ConnectionString, SiteID, Shove._Convert.StrToLong(dt.Rows[i]["SchemeID"].ToString(), 0), true, false, ref ReturnValue, ref ReturnDescription);

                        if ((Result < 0) || (ReturnValue < 0))
                        {
                            msg.Send("对所发送落地失败的代购票【作撤单】处理出错(SendTickets)：数据读写错误。票号：" + dt.Rows[i]["SchemeID"].ToString() + "，" + code + "，" + ReturnDescription);
                            log.Write("对所发送落地失败的代购票【作撤单】处理出错(SendTickets)：数据读写错误。票号：" + dt.Rows[i]["SchemeID"].ToString() + "，" + code + "，" + ReturnDescription);
                        }
                    }

                    continue;
                }

                if ("2048 3003 9999 1011".IndexOf(code) >= 0)     // 重复发送的投注票
                {
                    continue;
                }

                t_SchemesSendToCenter.Sends.Value = 100 + State;
                t_SchemesSendToCenter.Update(ConnectionString, "SchemeID = " + dt.Rows[i]["SchemeID"].ToString());

                if ("2010 2011 2012 2013 2014 2015 2016 2017 2018 2030 2031".IndexOf(code) >= 0)
                {
                    int Result = DAL.Procedures.P_QuashScheme(ConnectionString, SiteID, Shove._Convert.StrToLong(dt.Rows[i]["SchemeID"].ToString(), 0), true, false, ref ReturnValue, ref ReturnDescription);

                    if ((Result < 0) || (ReturnValue < 0))
                    {
                        msg.Send("对所发送落地失败的代购票【作撤单】处理出错(SendTickets)：数据读写错误。票号：" + dt.Rows[i]["SchemeID"].ToString() + "，" + code + "，" + ReturnDescription);
                        log.Write("对所发送落地失败的代购票【作撤单】处理出错(SendTickets)：数据读写错误。票号：" + dt.Rows[i]["SchemeID"].ToString() + "，" + code + "，" + ReturnDescription);
                    }
                }
            }
        }

        // 查询奖期状态
        private void QueryIsuseState()
        {
            // 查询的几组条件说明：
            //  1 有效期内未开奖、未开启的
            //  2 已截止未开奖的
            DataTable dt = new DAL.Views.V_Isuses().Open(ConnectionString, "[ID], LotteryID, [Name]", "((isOpened = 0 and (Getdate() between StartTime and EndTime) and State = 0) or (isOpened = 0 and Getdate() > EndTime and State < 5)) and PrintOutType = 102", "EndTime");

            if (dt == null)
            {
                msg.Send("期号状态查询错误(QueryIsuseState)。");
                log.Write("期号状态查询错误(QueryIsuseState)。");

                return;
            }

            if (dt.Rows.Count < 1)
            {
                return;
            }

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                if (i % 100 == 0)
                {
                    System.Threading.Thread.Sleep(1000);
                }

                #region 查询奖期

                string LotteryName = GetLotteryName(Shove._Convert.StrToInt(dt.Rows[i]["LotteryID"].ToString(), 0));
                string IsuseName = dt.Rows[i]["Name"].ToString();
                DateTime Now = DateTime.Now;

                string MessageID = ElectronTicket_HPSH_UserName + Now.ToString("yyyyMMdd") + Now.ToString("HHmmss") + (i % 100).ToString().PadLeft(2, '0');
                string Body = "<body><issueQuery><issue gameName=\"" + LotteryName + "\" number=\"" + IsuseName + "\"/></issueQuery></body>";
                string TimeStamp = Now.ToString("yyyyMMdd") + Now.ToString("HHmmss");

                string Message = "<?xml version=\"1.0\" encoding=\"GBK\"?>";
                Message += "<message version=\"1.0\" id=\"" + MessageID + "\">";
                Message += "<header>";
                Message += "<messengerID>" + ElectronTicket_HPSH_UserName + "</messengerID>";
                Message += "<timestamp>" + TimeStamp + "</timestamp>";
                Message += "<transactionType>102</transactionType>";
                Message += "<digest>" + Shove._Security.Encrypt.MD5(MessageID + TimeStamp + ElectronTicket_HPSH_UserPassword + Body, "gb2312") + "</digest>";
                Message += "</header>";
                Message += Body;
                Message += "</message>";

                WriteElectronTicketLog(true, "102", "transType=102&transMessage=" + Message);

                string ReceiveString = "";

                try
                {
                    ReceiveString = PublicFunction.Post(ElectronTicket_HPSH_Getway, "transType=102&transMessage=" + Message, TimeoutSeconds);
                }
                catch
                {
                    continue;
                }

                if (ReceiveString.Length <= 10)
                {
                    continue;
                }

                string[] t_strs = ReceiveString.Split('&');

                if ((t_strs == null) || (t_strs.Length < 2))
                {
                    continue;
                }

                string TransType = t_strs[0];
                string TransMessage = t_strs[1];

                TransType = TransType.Substring(10);
                TransMessage = TransMessage.Substring(13);

                WriteElectronTicketLog(false, TransType, ReceiveString);

                #endregion

                #region 处理结果

                System.Xml.XmlDocument XmlDoc = new XmlDocument();
                System.Xml.XmlNodeList nodes = null;
                System.Xml.XmlNodeList nodesIssue = null;

                try
                {
                    XmlDoc.Load(new StringReader(TransMessage));

                    nodes = XmlDoc.GetElementsByTagName("response");
                    nodesIssue = XmlDoc.GetElementsByTagName("issue");
                }
                catch
                {
                    continue;
                }

                if (nodes == null)
                {
                    continue;
                }

                if (nodes[0].Attributes["code"].Value != "0000")
                {
                    continue;
                }

                if (nodesIssue == null)
                {
                    continue;
                }

                DAL.Tables.T_Isuses t_Isuses = new DAL.Tables.T_Isuses();

                for (int j = 0; j < nodesIssue.Count; j++)
                {
                    LotteryName = nodesIssue[j].Attributes["gameName"].Value;
                    IsuseName = nodesIssue[j].Attributes["number"].Value;
                    string Status = nodesIssue[j].Attributes["status"].Value;
                    int LotteryID = GetLotteryID(LotteryName);
                    string WinNumber = "";

                    try
                    {
                        WinNumber = GetWinNumber(LotteryID, nodesIssue[j].Attributes["bonusCode"].Value);
                    }
                    catch
                    {

                    }

                    if ((LotteryID < 0) || (String.IsNullOrEmpty(IsuseName)))
                    {
                        continue;
                    }

                    DataTable dtIsuse = t_Isuses.Open(ConnectionString, "ID, State, WinLotteryNumber", "LotteryID = " + LotteryID.ToString() + " and [Name] = '" + IsuseName + "'", "");

                    if ((dtIsuse == null) || (dtIsuse.Rows.Count < 1))
                    {
                        continue;
                    }

                    bool isHasUpdate = false;

                    if (dtIsuse.Rows[0]["State"].ToString() != Status)
                    {
                        t_Isuses.State.Value = Status;
                        t_Isuses.StateUpdateTime.Value = DateTime.Now;

                        isHasUpdate = true;
                    }

                    if (!String.IsNullOrEmpty(WinNumber) && (dtIsuse.Rows[0]["WinLotteryNumber"].ToString() != WinNumber))
                    {
                        t_Isuses.WinLotteryNumber.Value = WinNumber;

                        isHasUpdate = true;

                        if (LotteryID == SLS.Lottery.SHSSL.ID)
                        {
                            DataTable dtWinTypes = new DAL.Tables.T_WinTypes().Open(ConnectionString, "", "LotteryID =" + LotteryID.ToString(), "");

                            double[] WinMoneyList = new double[dtWinTypes.Rows.Count * 2];

                            for (int k = 0; k < dtWinTypes.Rows.Count; k++)
                            {
                                WinMoneyList[k * 2] = Shove._Convert.StrToDouble(dtWinTypes.Rows[k]["DefaultMoney"].ToString(), 1);
                                WinMoneyList[k * 2 + 1] = Shove._Convert.StrToDouble(dtWinTypes.Rows[k]["DefaultMoneyNoWithTax"].ToString(), 1);
                            }

                            DataTable dtChaseTaskDetails = new DAL.Tables.T_ChaseTaskDetails().Open(ConnectionString, "", "IsuseID=" + dtIsuse.Rows[0]["ID"].ToString() + " and SchemeID IS NOT NULL", "");

                            for (int k = 0; k < dtChaseTaskDetails.Rows.Count; k++)
                            {
                                string LotteryNumber = dtChaseTaskDetails.Rows[k]["LotteryNumber"].ToString();

                                string Description = "";
                                double WinMoneyNoWithTax = 0;

                                double WinMoney = new SLS.Lottery()[LotteryID].ComputeWin(LotteryNumber, WinNumber, ref Description, ref WinMoneyNoWithTax, int.Parse(dtChaseTaskDetails.Rows[k]["PlayTypeID"].ToString()), WinMoneyList);

                                if (WinMoney < 1)
                                {
                                    continue;
                                }

                                int ReturnValue = 0;
                                string ReturnDescprtion = "";

                                if (DAL.Procedures.P_ChaseTaskStopWhenWin(ConnectionString, Shove._Convert.StrToLong(dtChaseTaskDetails.Rows[k]["SiteID"].ToString(), 1), Shove._Convert.StrToLong(dtChaseTaskDetails.Rows[k]["SchemeID"].ToString(), 0), WinMoney, ref ReturnValue, ref ReturnDescprtion) < 0)
                                {
                                    msg.Send("电子票撤销追号错误_P_ChaseTaskStopWhenWin。");
                                    log.Write("电子票撤销追号错误_P_ChaseTaskStopWhenWin。");
                                }
                            }
                        }
                    }

                    if (isHasUpdate)
                    {
                        t_Isuses.Update(ConnectionString, "LotteryID = " + LotteryID.ToString() + " and [Name] = '" + IsuseName + "'");
                    }
                }

                #endregion
            }
        }

        /////////////////////////////////////////////////////////////////////////////////////

        // 查询开奖信息并开奖。
        private void QueryIsuseOpen()
        {
            DataTable dt = new DAL.Views.V_Isuses().Open(ConnectionString, "[ID], LotteryID, [Name]", "IsOpened = 0 and State >= 5 and PrintOutType = 102", "EndTime");

            if (dt == null)
            {
                msg.Send("电子票查询开奖出错(QueryIsuseOpen)。");
                log.Write("电子票查询开奖出错(QueryIsuseOpen)。");

                return;
            }

            if (dt.Rows.Count < 1)
            {
                return;
            }

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                if (i % 100 == 0)
                {
                    System.Threading.Thread.Sleep(1000);
                }

                string IsuseID = dt.Rows[i]["ID"].ToString();
                string LotteryName = GetLotteryName(Shove._Convert.StrToInt(dt.Rows[i]["LotteryID"].ToString(), 0));
                string IsuseName = dt.Rows[i]["Name"].ToString();

                DateTime Now = DateTime.Now;
                string MessageID = ElectronTicket_HPSH_UserName + Now.ToString("yyyyMMdd") + Now.ToString("HHmmss") + (i % 100).ToString().PadLeft(2, '0');
                string Body = "<body><bonusQuery><issue gameName=\"" + LotteryName + "\" number=\"" + IsuseName + "\"/></bonusQuery></body>";
                string TimeStamp = Now.ToString("yyyyMMdd") + Now.ToString("HHmmss");

                string Message = "<?xml version=\"1.0\" encoding=\"GBK\"?>";
                Message += "<message version=\"1.0\" id=\"" + MessageID + "\">";
                Message += "<header>";
                Message += "<messengerID>" + ElectronTicket_HPSH_UserName + "</messengerID>";
                Message += "<timestamp>" + TimeStamp + "</timestamp>";
                Message += "<transactionType>106</transactionType>";
                Message += "<digest>" + Shove._Security.Encrypt.MD5(MessageID + TimeStamp + ElectronTicket_HPSH_UserPassword + Body, "gb2312") + "</digest>";
                Message += "</header>";
                Message += Body;
                Message += "</message>";

                WriteElectronTicketLog(true, "106", "transType=106&transMessage=" + Message);

                string ReceiveString = "";

                try
                {
                    ReceiveString = PublicFunction.Post(ElectronTicket_HPSH_Getway, "transType=106&transMessage=" + Message, TimeoutSeconds);
                }
                catch
                {
                    continue;
                }

                if (ReceiveString.Length <= 10)
                {
                    continue;
                }

                string[] t_strs = ReceiveString.Split('&');

                if ((t_strs == null) || (t_strs.Length < 2))
                {
                    continue;
                }

                string TransType = t_strs[0];
                string TransMessage = t_strs[1];

                TransType = TransType.Substring(10);
                TransMessage = TransMessage.Substring(13);

                WriteElectronTicketLog(false, TransType, ReceiveString);

                GetSprize(IsuseID, TransMessage);
            }
        }

        private void GetSprize(string IsuseID, string Transmessage)
        {
            System.Xml.XmlDocument XmlDoc = new XmlDocument();
            XmlDoc.Load(new StringReader(Transmessage));

            System.Xml.XmlNodeList nodes = XmlDoc.GetElementsByTagName("*");
            System.Xml.XmlNodeList nodesBonusItem = XmlDoc.GetElementsByTagName("bonusItem");
            System.Xml.XmlNodeList nodesIssue = XmlDoc.GetElementsByTagName("issue");

            string code = "";

            if (nodes == null)
            {
                return;
            }

            string BonusNumber = "";

            for (int j = 0; j < nodes.Count; j++)
            {
                if (nodes[j].Name.ToUpper() == "RESPONSE")
                {
                    code = nodes[j].Attributes["code"].Value;
                }

                if (nodes[j].Name.ToUpper() != "BONUSQUERYRESULT")
                {
                    continue;
                }

                BonusNumber = nodes[j].Attributes["bonusNumber"].InnerText;

            }

            if (code != "0000")
            {
                return;
            }

            if (nodesIssue == null)
            {
                return;
            }

            string number = nodesIssue[0].Attributes["number"].Value;
            string LotteryName = nodesIssue[0].Attributes["gameName"].Value;

            int LotteryID = GetLotteryID(LotteryName);
            string WinNumber = GetWinNumber(LotteryID, BonusNumber);

            DataTable dtIsuse = new DAL.Tables.T_Isuses().Open(ConnectionString, "", "[ID] = " + IsuseID + " and [Name] = '" + number + "' and LotteryID = " + LotteryID.ToString() + " and IsOpened = 0", "");

            if ((dtIsuse == null) || (dtIsuse.Rows.Count < 1))
            {
                return;
            }

            DAL.Tables.T_Isuses T_Isuses = new DAL.Tables.T_Isuses();

            T_Isuses.WinLotteryNumber.Value = WinNumber;
            T_Isuses.OpenOperatorID.Value = 1;
            T_Isuses.Update(ConnectionString, "[ID] = " + IsuseID + " and [Name] = '" + number + "' and LotteryID = " + LotteryID.ToString());

            DataTable dtWinTypesSSL = new DAL.Tables.T_WinTypes().Open(ConnectionString, "", " LotteryID =" + LotteryID.ToString(), "");

            if ((dtWinTypesSSL != null) && dtWinTypesSSL.Rows.Count > 0)
            {
                double[] WinMoneyListSSL = new double[dtWinTypesSSL.Rows.Count * 2];

                double DefaultMoneySSL = 0;
                double DefaultMoneyNoWithTaxSSL = 0;

                for (int i = 0; i < dtWinTypesSSL.Rows.Count; i++)
                {
                    DefaultMoneySSL = Shove._Convert.StrToDouble(dtWinTypesSSL.Rows[i]["DefaultMoney"].ToString(), 0);
                    DefaultMoneyNoWithTaxSSL = Shove._Convert.StrToDouble(dtWinTypesSSL.Rows[i]["DefaultMoneyNoWithTax"].ToString(), 0);

                    WinMoneyListSSL[i * 2] = DefaultMoneySSL == 0 ? 1 : DefaultMoneyNoWithTaxSSL;
                    WinMoneyListSSL[i * 2 + 1] = DefaultMoneyNoWithTaxSSL == 0 ? 1 : DefaultMoneyNoWithTaxSSL;
                }

                DataTable t_dtSchemes = new DAL.Tables.T_Schemes().Open(ConnectionString, "", "IsuseID = " + IsuseID + " and WinMoney = 0 and Buyed =1 and  isnull(Identifiers, '') = ''", "");

                string t_LotteryNumber = "";
                SLS.Lottery.LotteryBase t_lb = new SLS.Lottery()[LotteryID];

                string t_Description = "";
                double t_WinMoneyNoWithTax = 0;

                for (int i = 0; i < t_dtSchemes.Rows.Count; i++)
                {
                    t_LotteryNumber = t_dtSchemes.Rows[i]["LotteryNumber"].ToString();
                    t_Description = "";
                    t_WinMoneyNoWithTax = 0;

                    double WinMoney = t_lb.ComputeWin(t_LotteryNumber, WinNumber.Trim(), ref t_Description, ref t_WinMoneyNoWithTax, int.Parse(t_dtSchemes.Rows[i]["PlayTypeID"].ToString()), WinMoneyListSSL);

                    if (WinMoney > 0)
                    {
                        Shove.Database.MSSQL.ExecuteNonQuery(ConnectionString, "update T_Schemes set PreWinMoney = @p1, PreWinMoneyNoWithTax = @p2, WinMoney = @p3, WinMoneyNoWithTax = @p4, WinDescription = @p5 where [ID] = " + t_dtSchemes.Rows[i]["ID"].ToString(),
                            new Shove.Database.MSSQL.Parameter("p1", SqlDbType.Money, 0, ParameterDirection.Input, WinMoney * Shove._Convert.StrToInt(t_dtSchemes.Rows[i]["Multiple"].ToString(), 1)),
                            new Shove.Database.MSSQL.Parameter("p2", SqlDbType.Money, 0, ParameterDirection.Input, t_WinMoneyNoWithTax * Shove._Convert.StrToInt(t_dtSchemes.Rows[i]["Multiple"].ToString(), 1)),
                            new Shove.Database.MSSQL.Parameter("p3", SqlDbType.Money, 0, ParameterDirection.Input, WinMoney * Shove._Convert.StrToInt(t_dtSchemes.Rows[i]["Multiple"].ToString(), 1)),
                            new Shove.Database.MSSQL.Parameter("p4", SqlDbType.Money, 0, ParameterDirection.Input, t_WinMoneyNoWithTax * Shove._Convert.StrToInt(t_dtSchemes.Rows[i]["Multiple"].ToString(), 1)),
                            new Shove.Database.MSSQL.Parameter("p5", SqlDbType.VarChar, 0, ParameterDirection.Input, t_Description));

                        continue;
                    }
                }
            }

            string BonusXML = "<Schemes>";

            if ((nodesBonusItem != null) && (nodesBonusItem.Count > 0))
            {
                string bonusItemXML = Transmessage.Substring(Transmessage.IndexOf("<bonusQueryResult"), Transmessage.LastIndexOf("</response>") - Transmessage.IndexOf("<bonusQueryResult"));
                DataSet dsXML = new DataSet();

                try
                {
                    dsXML.ReadXml(new StringReader(bonusItemXML));
                }
                catch (Exception e)
                {
                    new Log("ElectronTicket\\HPSH").Write("电子票开奖，第 " + number + " 期解析开奖数据错误：" + e.Message);

                    return;
                }

                if ((dsXML == null) || (dsXML.Tables.Count < 3))
                {
                    new Log("ElectronTicket\\HPSH").Write("电子票开奖，第 " + number + " 期开奖数据格式不符合要求。");

                    return;
                }

                DataTable dtTickets = dsXML.Tables[2];
                DataTable dtSchemes = MSSQL.Select(ConnectionString, "SELECT SchemeID, 0 AS AgentID, SchemesMultiple as Multiple, Identifiers FROM V_SchemesSendToCenter WHERE (IsuseID = " + IsuseID + ")");

                if (dtSchemes == null)
                {
                    new Log("ElectronTicket\\HPSH").Write("电子票开奖，第 " + number + " 期，读取本地方案错误。");

                    return;
                }

                try
                {
                    var query1 = from NewDtTickets in dtTickets.AsEnumerable()
                                 join NewdtScheme in dtSchemes.AsEnumerable()
                                 on NewDtTickets.Field<string>("ticketID") equals NewdtScheme.Field<string>("Identifiers")
                                 select new
                                 {
                                     ID = NewdtScheme.Field<long>("SchemeID"),
                                     AgentID = NewdtScheme.Field<long>("AgentID"),
                                     Multiple = NewdtScheme.Field<int>("Multiple"),
                                     Bonus = Shove._Convert.StrToDouble(NewDtTickets.Field<string>("money"), 0),
                                     BonusLevel = NewDtTickets.Field<string>("bonusLevel"),
                                     Size = Shove._Convert.StrToInt(NewDtTickets.Field<string>("size"), 1)
                                 };

                    var query2 = from NewDt in query1.AsQueryable()
                                 group NewDt by new { NewDt.ID, NewDt.BonusLevel, NewDt.AgentID, NewDt.Multiple } into gg
                                 select new
                                 {
                                     ID = gg.Key.ID,
                                     AgentID = gg.Key.AgentID,
                                     Multiple = gg.Key.Multiple,
                                     Bonus = gg.Sum(NewDt => NewDt.Bonus),
                                     BonusLevel = GetSchemeWinDescription(gg.Key.BonusLevel, LotteryID, (gg.Sum(NewDt => NewDt.Size) / gg.Key.Multiple))
                                 };

                    var query3 = from NewDt in query2.AsQueryable()
                                 group NewDt by new { NewDt.ID, NewDt.Multiple, NewDt.AgentID } into t_dtSchemes
                                 select new
                                 {
                                     SchemeID = t_dtSchemes.Key.ID,
                                     AgentID = t_dtSchemes.Key.AgentID,
                                     Multiple = t_dtSchemes.Key.Multiple,
                                     Bonus = t_dtSchemes.Sum(NewDt => NewDt.Bonus),
                                     BonusLevel = t_dtSchemes.Merge(NewDt => NewDt.BonusLevel) + ((t_dtSchemes.Key.Multiple != 1) ? "(" + t_dtSchemes.Key.Multiple.ToString() + "倍)" : "")
                                 };

                    foreach (var Scheme in query3)
                    {
                            BonusXML += "<Scheme SchemeID=\"" + Scheme.SchemeID.ToString() + "\" WinMoney=\"" + Scheme.Bonus.ToString() + "\" WinDescription=\"" + Scheme.BonusLevel + "\" />";
                    }
                }
                catch (Exception e)
                {
                    new Log("ElectronTicket\\HPSH").Write("电子票开奖，第 " + number + " 期详细中奖数据解析错误：" + e.Message);

                    return;
                }
            }

            BonusXML += "</Schemes>";

            dtIsuse = new DAL.Tables.T_Isuses().Open(ConnectionString, "", "[ID] = " + IsuseID + " and IsOpened = 0", "");

            if ((dtIsuse == null) || (dtIsuse.Rows.Count < 1))
            {
                return;
            }

            int ReturnValue = 0;
            string ReturnDescription = "";

            DataSet ds = null;
            int Times = 0;
            int Result = -1;

            while ((Result < 0) && (Times < 5))
            {
                ReturnValue = 0;
                ReturnDescription = "";

                Result = DAL.Procedures.P_ElectronTicketWin(ConnectionString, ref ds, Shove._Convert.StrToLong(IsuseID, 0), BonusXML, ref ReturnValue, ref ReturnDescription);

                if (Result < 0)
                {
                    new Log("ElectronTicket\\HPSH").Write("电子票第 " + (Times + 1).ToString() + " 次派奖出现错误(IsuseOpenNotice) 期号为: " + number + "，彩种为: " + LotteryID.ToString());
                    Times++;

                    if (Times < 5)
                    {
                        System.Threading.Thread.Sleep(10000);
                    }

                    continue;
                }
            }

            if (ReturnValue < 0)
            {
                log.Write(ReturnDescription);

                return;
            }

            //PublicFunction.SendWinNotification(ds);

            DataTable dtWinTypes = new DAL.Tables.T_WinTypes().Open(ConnectionString, "", " LotteryID =" + LotteryID.ToString(), "");

            if ((dtWinTypes == null) || dtWinTypes.Rows.Count < 1)
            {
                msg.Send("执行电子票--获取彩种: " + LotteryID.ToString() + "，奖金等级时出现错误!");
                log.Write("执行电子票--获取彩种: " + LotteryID.ToString() + "，奖金等级时出现错误!");

                return;
            }

            double[] WinMoneyList = new double[dtWinTypes.Rows.Count * 2];

            double DefaultMoney = 0;
            double DefaultMoneyNoWithTax = 0;

            for (int i = 0; i < dtWinTypes.Rows.Count; i++)
            {
                DefaultMoney = Shove._Convert.StrToDouble(dtWinTypes.Rows[i]["DefaultMoney"].ToString(), 0);
                DefaultMoneyNoWithTax = Shove._Convert.StrToDouble(dtWinTypes.Rows[i]["DefaultMoneyNoWithTax"].ToString(), 0);

                WinMoneyList[i * 2] = DefaultMoney == 0 ? 1 : DefaultMoneyNoWithTax;
                WinMoneyList[i * 2 + 1] = DefaultMoneyNoWithTax == 0 ? 1 : DefaultMoneyNoWithTax;
            }

            DataTable dtSchemesWithTaskDetails = new DAL.Views.V_Schemes().Open(ConnectionString, "", " IsuseName = '" + number + "' and LotteryID = " + LotteryID.ToString() + " and WinMoney = 0  and Buyed = 0 and ID in ( select ID from V_ChaseTaskDetails where IsuseName = '" + number + "' and LotteryID = " + LotteryID.ToString() + ")", "");

            string LotteryNumber = "";
            SLS.Lottery.LotteryBase lb = new SLS.Lottery()[LotteryID];

            string Description = "";
            double WinMoneyNoWithTax = 0;

            for (int i = 0; i < dtSchemesWithTaskDetails.Rows.Count; i++)
            {
                LotteryNumber = dtSchemesWithTaskDetails.Rows[i]["LotteryNumber"].ToString();

                Description = "";
                WinMoneyNoWithTax = 0;

                double WinMoney = lb.ComputeWin(LotteryNumber, WinNumber.Trim(), ref Description, ref WinMoneyNoWithTax, int.Parse(dtSchemesWithTaskDetails.Rows[i]["PlayTypeID"].ToString()), WinMoneyList);

                if (WinMoney > 0)
                {
                    if (DAL.Procedures.P_ChaseTaskStopWhenWin(ConnectionString, Shove._Convert.StrToLong(dtSchemesWithTaskDetails.Rows[i]["SiteID"].ToString(), 0), Shove._Convert.StrToLong(dtSchemesWithTaskDetails.Rows[i]["ID"].ToString(), 0), WinMoney, ref ReturnValue, ref ReturnDescription) < 0)
                    {
                        msg.Send("执行电子票--判断是否停止追号的时候出现错误");
                        log.Write("执行电子票--判断是否停止追号的时候出现错误");

                        continue;
                    }
                }
            }
        }

        #endregion

        /////////////////////////////////////////////////////////////////////////////////////

        // 返回成功信息
        private void ReNotice(string MessageID, string Type)
        {
            DateTime Now = DateTime.Now;

            string Body = "<body><response code=\"0000\" message=\"成功，系统处理正常\"/></body>";

            string TimeStamp = Now.ToString("yyyyMMdd") + Now.ToString("HHmmss");

            string Message = "<?xml version=\"1.0\" encoding=\"GBK\"?>";
            Message += "<message version=\"1.0\" id=\"" + MessageID + "\">";
            Message += "<header>";
            Message += "<messengerID>" + ElectronTicket_HPSH_UserName + "</messengerID>";
            Message += "<timestamp>" + TimeStamp + "</timestamp>";
            Message += "<transactionType>" + Type + "</transactionType>";
            Message += "<digest>" + Shove._Security.Encrypt.MD5(MessageID + TimeStamp + ElectronTicket_HPSH_UserPassword + Body, "gb2312") + "</digest>";
            Message += "</header>";
            Message += Body;
            Message += "</message>";

            WriteElectronTicketLog(true, Type, "transType=" + Type + "&transMessage=" + Message);

            PublicFunction.Post(ElectronTicket_HPSH_Getway, "transType=" + Type + "&transMessage=" + Message, TimeoutSeconds);
        }

        // 查询奖期状态
        private void GetLotteryIsuses()
        {
            // 查询的几组条件说明：
            //  1 有效期内未开奖、未开启的
            //  2 已截止未开奖的
            DataTable dt = new DAL.Tables.T_Lotteries().Open(ConnectionString, "[ID]", " PrintOutType = 102", "");

            if (dt == null)
            {
                msg.Send("期号状态查询错误(QueryIsuseState)。");
                log.Write("期号状态查询错误(QueryIsuseState)。");

                return;
            }

            if (dt.Rows.Count < 1)
            {
                return;
            }

            for (int i = 0; i < dt.Rows.Count; i++)
            {
                if (i % 100 == 0)
                {
                    System.Threading.Thread.Sleep(1000);
                }

                #region 查询奖期

                string LotteryName = GetLotteryName(Shove._Convert.StrToInt(dt.Rows[i]["ID"].ToString(), 0));
                string IsuseName = "";
                DateTime Now = DateTime.Now;

                string MessageID = ElectronTicket_HPSH_UserName + Now.ToString("yyyyMMdd") + Now.ToString("HHmmss") + (i % 100).ToString().PadLeft(2, '0');
                string Body = "<body><issueQuery><issue gameName=\"" + LotteryName + "\" number=\"" + IsuseName + "\"/></issueQuery></body>";
                string TimeStamp = Now.ToString("yyyyMMdd") + Now.ToString("HHmmss");

                string Message = "<?xml version=\"1.0\" encoding=\"GBK\"?>";
                Message += "<message version=\"1.0\" id=\"" + MessageID + "\">";
                Message += "<header>";
                Message += "<messengerID>" + ElectronTicket_HPSH_UserName + "</messengerID>";
                Message += "<timestamp>" + TimeStamp + "</timestamp>";
                Message += "<transactionType>102</transactionType>";
                Message += "<digest>" + Shove._Security.Encrypt.MD5(MessageID + TimeStamp + ElectronTicket_HPSH_UserPassword + Body, "gb2312") + "</digest>";
                Message += "</header>";
                Message += Body;
                Message += "</message>";

                WriteElectronTicketLog(true, "102", "transType=102&transMessage=" + Message);

                string ReceiveString = "";

                try
                {
                    ReceiveString = PublicFunction.Post(ElectronTicket_HPSH_Getway, "transType=102&transMessage=" + Message, TimeoutSeconds);
                }
                catch
                {
                    continue;
                }

                if (ReceiveString.Length <= 10)
                {
                    continue;
                }

                string[] t_strs = ReceiveString.Split('&');

                if ((t_strs == null) || (t_strs.Length < 2))
                {
                    continue;
                }

                string TransType = t_strs[0];
                string TransMessage = t_strs[1];

                TransType = TransType.Substring(10);
                TransMessage = TransMessage.Substring(13);

                WriteElectronTicketLog(false, TransType, ReceiveString);

                #endregion
            }
        }

        #region 公共方法

        private string GetFromXPath(string XML, string XPath)
        {
            if (XML.Trim() == "")
                return "";

            string Result = "";
            try
            {
                XPathDocument doc = new XPathDocument(new StringReader(XML));
                XPathNavigator nav = doc.CreateNavigator();
                XPathNodeIterator nodes = nav.Select(XPath);
                while (nodes.MoveNext())
                    Result += nodes.Current.Value;
            }
            catch//(Exception ee)
            {
                return "";
                //return ee.Message;
            }

            return Result;
        }

        private string GetLotteryName(int LotteryID)
        {
            switch (LotteryID)
            {
                case 5:
                    return "ssq";
                case 6:
                    return "3d";
                case 13:
                    return "307";
                case 29:
                    return "ssl";
                case 58:
                    return "601";
                case 59:
                    return "155";
                case 60:
                    return "4d";
                default:
                    return "";
            }
        }

        private int GetLotteryID(string LotteryName)
        {
            switch (LotteryName)
            {
                case "ssq":
                    return 5;
                case "3d":
                    return 6;
                case "307":
                    return 13;
                case "ssl":
                    return 29;
                case "601":
                    return 58;
                case "155":
                    return 59;
                case "4d":
                    return 60;
                default:
                    return -1;
            }
        }

        //写日志文件
        public void WriteElectronTicketLog(bool isSend, string TransType, string TransMessage)
        {
            log.Write("isSend: " + isSend.ToString() + "\tTransType: " + TransType + "\t" + TransMessage);
        }

        #endregion

        #region 分析中奖描述、奖等

        private string GetSchemeWinDescription(string WinList, int LotteryID, int size)
        {
            string Description = "";

            Description += (GetWinDescription(LotteryID, WinList) + size.ToString() + "注");

            return Description;
        }

        private class CompareToLength : IComparer
        {
            int IComparer.Compare(Object x, Object y)
            {
                long _x = Shove._Convert.StrToLong(x.ToString(), -1);
                long _y = Shove._Convert.StrToLong(y.ToString(), -1);

                return _x.CompareTo(_y);
            }
        }

        private string GetWinNumber(int LotteryID, string WinNumber)
        {
            switch (LotteryID)
            {
                case 5:
                    return WinNumber.Replace(",", " ").Replace("#", " + ");
                case 6:
                    return WinNumber.Replace(",", "");
                case 13:
                    return WinNumber.Replace(",", " ").Replace("#", " + ");
                case 29:
                    return WinNumber.Replace(",", "");
                case 58:
                    return WinNumber.Replace(",", "").Replace("#01", "+鼠").Replace("#02", "+牛").Replace("#03", "+虎").Replace("#04", "+兔").Replace("#05", "+龙").Replace("#06", "+蛇").Replace("#07", "+马").Replace("#08", "+羊").Replace("#09", "+猴").Replace("#10", "+鸡").Replace("#11", "+狗").Replace("#12", "+猪");
                case 59:
                    return WinNumber.Replace(",", " ").Replace("#", " ");
                case 60:
                    return WinNumber.Replace(",", "");
                default:
                    return WinNumber;
            }
        }

        private string GetWinDescription(int LotteryID, string Rank)
        {
            switch (LotteryID)
            {
                case 5:
                    switch (Rank)
                    {
                        case "1":
                            return "一等奖";
                        case "2":
                            return "二等奖";
                        case "3":
                            return "三等奖";
                        case "4":
                            return "四等奖";
                        case "5":
                            return "五等奖";
                        case "6":
                            return "六等奖";
                        case "7":
                            return "快乐星期天";
                        default:
                            return "";
                    }
                case 6:
                    switch (Rank)
                    {
                        case "1":
                            return "直选奖";
                        case "2":
                            return "组3奖";
                        case "3":
                            return "组6奖";
                        default:
                            return "";
                    }
                case 13:
                    switch (Rank)
                    {
                        case "1":
                            return "一等奖";
                        case "2":
                            return "二等奖";
                        case "3":
                            return "三等奖";
                        case "4":
                            return "四等奖";
                        case "5":
                            return "五等奖";
                        case "6":
                            return "六等奖";
                        case "7":
                            return "七等奖";
                        default:
                            return "";
                    }
                case 29:
                    switch (Rank)
                    {
                        case "1":
                            return "直选奖";
                        case "2":
                            return "组三奖";
                        case "3":
                            return "组六奖";
                        case "4":
                            return "前二奖";
                        case "5":
                            return "后二奖";
                        case "6":
                            return "前一奖";
                        case "7":
                            return "后一奖";
                        default:
                            return "";
                    }
                case 58:
                    switch (Rank)
                    {
                        case "1":
                            return "一等奖";
                        case "2":
                            return "二等奖";
                        case "3":
                            return "三等奖";
                        case "4":
                            return "四等奖";
                        case "5":
                            return "五等奖";
                        case "6":
                            return "六等奖";
                        default:
                            return "";
                    }
                case 59:
                    switch (Rank)
                    {
                        case "0":
                            return "特等奖";
                        case "1":
                            return "一等奖";
                        case "2":
                            return "二等奖";
                        case "3":
                            return "派送奖";
                        default:
                            return "";
                    }
                case 60:
                    switch (Rank)
                    {
                        case "1":
                            return "直选奖";
                        case "2":
                            return "组四奖";
                        case "3":
                            return "组六奖";
                        case "4":
                            return "组12奖";
                        case "5":
                            return "组24奖";
                        default:
                            return "";
                    }
                default:
                    return "";
            }
        }

        #endregion
    }
}