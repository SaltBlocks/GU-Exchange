﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GU_Exchange
{
    internal interface IMXlib
    {
        #region Constants.
        public const string IMX_SEED_MESSAGE = "Only sign this request if you’ve initiated an action with Immutable X.";
        public const string IMX_LINK_MESSAGE = "Only sign this key linking request from Immutable X";
        #endregion

        #region Structs.
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct Fee
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 43)]
            public string address;
            public double percentage;
        }
        #endregion

        // DLL functions used to interact with IMX.
        #region ETH key and address generation.
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr eth_generate_key(IntPtr result_buffer, int buffer_size);
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr eth_get_address(string eth_priv_str, [Out] IntPtr result_buffer, int buffer_size);
        #endregion
        #region Registration of ETH address with IMX.
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr imx_register_address(string eth_priv_str, IntPtr result_buffer, int buffer_size);
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr imx_register_address_presigned(string eth_address_str, string link_sig_str, string seed_sig_str, IntPtr result_buffer, int buffer_size);
        #endregion
        #region Buying orders on the IMX orderbook.
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr imx_buy_order(string order_id_str, double price_limit, Fee[] fees, int fee_count, string eth_priv_str, IntPtr result_buffer, int buffer_size);
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr imx_request_buy_order(string order_id_str, string eth_address_str, Fee[] fees, int fee_count, IntPtr result_buffer, int buffer_size);
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr imx_finish_buy_order(string nonce_str, double price_limit, string imx_seed_sig_str, string imx_transaction_sig_str, IntPtr result_buffer, int buffer_size);
        #endregion

        public static string? IntPtrToString(IntPtr ptr)
        {

            return Marshal.PtrToStringAnsi(ptr);
        }

        public static string? IntPtrToUtf8String(IntPtr ptr)
        {
            return Marshal.PtrToStringUTF8(ptr);
        }
    }
}
