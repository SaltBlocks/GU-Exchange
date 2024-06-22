using System;
using System.Runtime.InteropServices;

namespace GU_Exchange.Helpers
{
    public interface IMXlib
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

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct NFT
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 43)]
            public string token_address;
            public ulong token_id;
        };
        #endregion

        // DLL functions used to interact with IMX.
        #region ETH key and address generation.
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr eth_generate_key(IntPtr result_buffer, int buffer_size);
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr eth_get_address(string eth_priv_str, [Out] IntPtr result_buffer, int buffer_size);
        #endregion
        #region Fetch token information.
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern double imx_get_token_trade_fee(string token_address_str, string token_id_str);
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
        #region Creating listings on the IMX orderbook.
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr imx_sell_nft(string nft_address_str, string nft_id_str, string token_id_str, double price, Fee[] fees, int fee_count, string eth_priv_str, IntPtr result_buffer, int buffer_size);
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr imx_request_sell_nft(string nft_address_str, string nft_id_str, string token_id_str, double price, Fee[] fees, int fee_count, string seller_address_str, IntPtr result_buffer, int buffer_size);
        #endregion
        #region Finish sale or offer creation.
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr imx_finish_sell_or_offer_nft(string nonce_str, string imx_seed_sig_str, string imx_transaction_sig_str, IntPtr result_buffer, int buffer_size);
        #endregion
        #region Cancel Order.
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr imx_cancel_order(string order_id_str, string eth_priv_str, IntPtr result_buffer, int buffer_size);
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr imx_request_cancel_order(string order_id_str, IntPtr result_buffer, int buffer_size);
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr imx_finish_cancel_order(string order_id_str, string eth_address_str, string imx_seed_sig_str, string imx_transaction_sig_str, IntPtr result_buffer, int buffer_size);
        #endregion
        #region Transfer card(s).
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr imx_transfer_nfts(NFT[] nfts, int nft_count, string receiver_address_str, string eth_priv_str, IntPtr result_buffer, int buffer_size);
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr imx_request_transfer_nfts(NFT[] nfts, int nft_count, string receiver_address, string sender_address, IntPtr result_buffer, int buffer_size);
        #endregion
        #region Transfer currency.
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr imx_transfer_token(string token_id_str, double amount, string receiver_address_str, string eth_priv_str, IntPtr result_buffer, int buffer_size);
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr imx_request_transfer_token(string token_id_str, double amount, string receiver_address_str, string sender_address_str, IntPtr result_buffer, int buffer_size);
        #endregion
        #region Finish transfer.
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr imx_finish_transfer(string nonce_str, string imx_seed_sig_str, string imx_transaction_sig_str, IntPtr result_buffer, int buffer_size);
        #endregion

        #region Pointer convertion.
        public static string? IntPtrToString(IntPtr ptr)
        {
            return Marshal.PtrToStringAnsi(ptr);
        }

        public static string? IntPtrToUtf8String(IntPtr ptr)
        {
            return Marshal.PtrToStringUTF8(ptr);
        }
        #endregion
    }
}
