using System;
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

        // DLL functions used to interact with IMX.
        #region ETH key and address generation.
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr eth_generate_key([Out] char[] result_buffer, int buffer_size);
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr eth_get_address([In] char[] eth_priv_str, [Out] char[] result_buffer, int buffer_size);
        #endregion
        #region Registration of ETH address with IMX.
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr imx_register_address([In] char[] eth_priv_str, [Out] char[] result_buffer, int buffer_size);
        [DllImport("IMXlib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr imx_register_address_presigned([In] char[] eth_address_str, [In] char[] link_sig_str, [In] char[] seed_sig_str, [Out] char[] result_buffer, int buffer_size);
        #endregion
    }
}
