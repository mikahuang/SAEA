﻿/****************************************************************************
*Copyright (c) 2018 Microsoft All Rights Reserved.
*CLR版本： 4.0.30319.42000
*机器名称：WENLI-PC
*公司名称：Microsoft
*命名空间：SAEA.RPC.Common
*文件名： RPCInovker
*版本号： V1.0.0.0
*唯一标识：289c03b9-3910-4e15-8072-93243507689c
*当前的用户域：WENLI-PC
*创建人： yswenli
*电子邮箱：wenguoli_520@qq.com
*创建时间：2018/5/17 14:11:30
*描述：
*
*=====================================================================
*修改标记
*修改时间：2018/5/17 14:11:30
*修改人： yswenli
*版本号： V1.0.0.0
*描述：
*
*****************************************************************************/
using SAEA.Commom;
using SAEA.RPC.Model;
using SAEA.RPC.Net;
using SAEA.RPC.Serialize;
using SAEA.Sockets.Interface;
using System;
using System.Linq;
using System.Reflection;

namespace SAEA.RPC.Common
{
    /// <summary>
    /// RPC将远程调用反转到本地服务
    /// </summary>
    public class RPCReversal
    {
        static object _locker = new object();

        /// <summary>
        /// 执行方法
        /// </summary>
        /// <param name="method"></param>
        /// <param name="methodInvoker"></param>
        /// <param name="obj"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        private static object ReversalMethod(MethodInfo method, FastInvoke.FastInvokeHandler methodInvoker, object obj, object[] args)
        {
            object result = null;
            try
            {
                var inputs = args;

                var @params = method.GetParameters();

                if (@params == null || @params.Length == 0)
                {
                    inputs = null;
                }
                result = methodInvoker.Invoke(obj, inputs);
            }
            catch (Exception ex)
            {
                throw new RPCPamarsException($"{obj}/{method.Name},出现异常：{ex.Message}", ex);
            }
            return result;
        }


        public static object Reversal(IUserToken userToken, string serviceName, string methodName, object[] inputs)
        {
            lock (_locker)
            {
                try
                {
                    var serviceInfo = RPCMapping.Get(serviceName, methodName);

                    if (serviceInfo == null)
                    {
                        throw new RPCNotFundException($"当前请求找不到:{serviceName}/{methodName}", null);
                    }

                    var nargs = new object[] { userToken, serviceName, methodName, inputs };

                    if (serviceInfo.FilterAtrrs != null && serviceInfo.FilterAtrrs.Count > 0)
                    {
                        foreach (var arr in serviceInfo.FilterAtrrs)
                        {
                            var goOn = (bool)FastInvoke.GetMethodInvoker(arr.GetType().GetMethod("OnActionExecuting")).Invoke(arr, nargs.ToArray());

                            if (!goOn)
                            {
                                return new RPCNotFundException("当前逻辑已被拦截！", null);
                            }
                        }
                    }

                    if (serviceInfo.ActionFilterAtrrs != null && serviceInfo.ActionFilterAtrrs.Count > 0)
                    {
                        foreach (var arr in serviceInfo.ActionFilterAtrrs)
                        {
                            var goOn = (bool)FastInvoke.GetMethodInvoker(arr.GetType().GetMethod("OnActionExecuting")).Invoke(arr, nargs.ToArray());

                            if (!goOn)
                            {
                                return new RPCNotFundException("当前逻辑已被拦截！", null);
                            }
                        }
                    }

                    var result = ReversalMethod(serviceInfo.Method, serviceInfo.MethodInvoker, serviceInfo.Instance, inputs);

                    nargs = new object[] { userToken, serviceName, methodName, inputs, result };

                    if (serviceInfo.FilterAtrrs != null && serviceInfo.FilterAtrrs.Count > 0)
                    {
                        foreach (var arr in serviceInfo.FilterAtrrs)
                        {
                            FastInvoke.GetMethodInvoker(arr.GetType().GetMethod("OnActionExecuted")).Invoke(arr, nargs);
                        }
                    }

                    if (serviceInfo.ActionFilterAtrrs != null && serviceInfo.ActionFilterAtrrs.Count > 0)
                    {
                        foreach (var arr in serviceInfo.FilterAtrrs)
                        {
                            FastInvoke.GetMethodInvoker(arr.GetType().GetMethod("OnActionExecuted")).Invoke(arr, nargs);
                        }
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("找不到此rpc方法"))
                    {
                        return new RPCNotFundException("找不到此rpc方法", ex);
                    }
                    else
                    {
                        return new RPCNotFundException("找不到此rpc方法", ex);
                    }
                }
            }
        }

        /// <summary>
        /// 反转到具体的方法上
        /// </summary>
        /// <param name="userToken"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static byte[] Reversal(IUserToken userToken, RSocketMsg msg)
        {
            byte[] result = null;
            try
            {
                object[] inputs = null;

                if (msg.Data != null)
                {
                    try
                    {
                        var ptypes = RPCMapping.Get(msg.ServiceName, msg.MethodName).Pamars.Values.ToArray();

                        inputs = ParamsSerializeUtil.Deserialize(ptypes, msg.Data);
                    }
                    catch (Exception ex)
                    {
                        throw new RPCNotFundException("找不到此服务", ex);
                    }
                }

                var r = Reversal(userToken, msg.ServiceName, msg.MethodName, inputs);

                if (r != null)
                {
                    return ParamsSerializeUtil.Serialize(r);
                }
            }
            catch (Exception ex)
            {
                throw new RPCPamarsException("RPCInovker.Reversal error:" + ex.Message, ex);
            }
            return result;

        }
    }
}
