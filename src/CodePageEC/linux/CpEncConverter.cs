using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;                  // for RegistryKey
using ECInterfaces;                     // for IEncConverter

namespace SilEncConverters40
{
    /// <summary>
    /// Managed Code Page EncConverter
    /// </summary>
    //[GuidAttribute("F91EBC49-1019-4ff3-B143-A84E6081A472")]
    // normally these subclasses are treated as the base class (i.e. the 
    //  client can use them orthogonally as IEncConverter interface pointers
    //  so normally these individual subclasses would be invisible), but if 
    //  we add 'ComVisible = false', then it doesn't get the registry 
    //  'HKEY_CLASSES_ROOT\SilEncConverters40.TecEncConverter' which is the basis of 
    //  how it is started (see EncConverters.AddEx).
    // [ComVisible(false)] 
	public class CpEncConverter : EncConverter
    {
        #region Member Variable Definitions
        private const int   CP_UTF8  = 65001;
        private const int   CP_UTF16 = 1200;

        private int     m_nCodePage;    // the code page to convert with
        private bool    m_bToWide;      // we have to keep track of the direction since it might be different than m_bForward
        
        public const string strDisplayName = "Code Page Converter";
        public const string strHtmlFilename = "Code Page Converter Plug-in About box.mht";
        #endregion Member Variable Definitions

        #region Initialization
        public CpEncConverter() : base(typeof(CpEncConverter).FullName,EncConverters.strTypeSILcp)
        {
            Console.WriteLine("Cp EC Initialize");
        }

        public override void Initialize(string converterName, string converterSpec, 
			ref string lhsEncodingID, ref string rhsEncodingID, ref ConvType conversionType, 
            ref Int32 processTypeFlags, Int32 codePageInput, Int32 codePageOutput, bool bAdding)
        {
            Console.WriteLine("Cp EC Initialize BEGIN");
            base.Initialize(converterName, converterSpec, ref lhsEncodingID, ref rhsEncodingID, ref conversionType, ref processTypeFlags, codePageInput, codePageOutput, bAdding );
            m_nCodePage = System.Convert.ToInt32(ConverterIdentifier);
            
            // the UTF16 code page is a special case: ConvType must be Uni <> Uni
            if( m_nCodePage == CP_UTF16 )
            {
                if( String.IsNullOrEmpty(LeftEncodingID) )
                    lhsEncodingID = m_strLhsEncodingID = "utf-16";
                if( String.IsNullOrEmpty(RightEncodingID) )
                    rhsEncodingID = m_strRhsEncodingID = EncConverters.strDefUnicodeEncoding;
                
                // This only works if we consider UTF16 to be unicode; not legacy
                conversionType = m_eConversionType = ConvType.Unicode_to_from_Unicode;
            }

            // the rest are all "UnicodeEncodingConversion"s (by definition). Also, use the word 
            // "UNICODE" concatenated to the legacy encoding name
            else 
            {
                processTypeFlags = m_lProcessType |= (int)ProcessTypeFlags.UnicodeEncodingConversion;
                if( String.IsNullOrEmpty(RightEncodingID) )
                {
                    rhsEncodingID = m_strRhsEncodingID = EncConverters.strDefUnicodeEncoding;
                    if( !String.IsNullOrEmpty(lhsEncodingID) )
                        rhsEncodingID += " " + lhsEncodingID;
                }
            }

            // if it wasn't set above, then it's Legacy <> Unicode
            if( conversionType == ConvType.Unknown )
                conversionType = m_eConversionType = ConvType.Legacy_to_from_Unicode;

            // finally, it is also a "Code Page" conversion process type
            processTypeFlags = m_lProcessType |= (int)ProcessTypeFlags.CodePageConversion;
            Console.WriteLine("Cp EC Initialize END");
        }

        protected override EncodingForm  DefaultUnicodeEncForm(bool bForward, bool bLHS)
        { 
            // the only left-hand-side for forwarding conversion unicode possible is UTF16.
            if( bLHS )
            {
                // Note: This is opposite in Linux from that of Windows.
                if( bForward )
                    return EncodingForm.UTF16; 
                else
                    return EncodingForm.UTF8String;
            }
            else    // wrt. rhs
            {
                if( bForward )
                    return EncodingForm.UTF8String;
                else
                    return EncodingForm.UTF16; 
            }
        }
        #endregion Initialization

        #region DLLImport Statements
        // libiconv.so (preferred)
        // or libc.so, iconv_open iconv_close iconv
        [DllImport("iconv")]
        static extern unsafe IntPtr iconv_open(string enc1, string enc2);

        [DllImport("iconv", SetLastError=true)]
        static extern unsafe int iconv(
            IntPtr cd,
            char * inbuf,
            ref int inbytesleft,
            char * outbuf,
            ref int howMany);

        [DllImport("iconv")]
        static extern unsafe int iconv_close(IntPtr cd);
        #endregion DLLImport Statements

        #region Abstract Base Class Overrides
        protected override void PreConvert
            (
            EncodingForm        eInEncodingForm,
            ref EncodingForm    eInFormEngine,
            EncodingForm        eOutEncodingForm,
            ref EncodingForm    eOutFormEngine,
            ref NormalizeFlags  eNormalizeOutput,
            bool                bForward
            ) 
        {
            // let the base class do it's thing first
            base.PreConvert( eInEncodingForm, ref eInFormEngine,
                eOutEncodingForm, ref eOutFormEngine,
                ref eNormalizeOutput, bForward);

            // we have to know what the forward flag state is (and we can't use m_bForward because
            //	that might be different (e.g. if this was called from ConvertEx).
            m_bToWide = bForward;

            // check if this is the special UTF8 code page, and if so, request that the engine
            //	form be UTF8Bytes (this is the one code page converter where both sides are 
            //	Unicode.
            if( m_bToWide )
            {
                // going "to wide" means the output form required by the engine is UTF16.
                //eOutFormEngine = EncodingForm.UTF16;  // Windows
                //if( m_nCodePage == CP_UTF8 )
                //    eInFormEngine = EncodingForm.UTF8Bytes;
                eOutFormEngine = EncodingForm.UTF8Bytes;    // Linux
                if( m_nCodePage == CP_UTF16 )
                    eInFormEngine = EncodingForm.UTF16;

            }
            else
            {
                // going "from wide" means the input form required by the engine is UTF16.
                //eInFormEngine = EncodingForm.UTF16; // Windows
                //if( m_nCodePage == CP_UTF8 )
                //    eOutFormEngine = EncodingForm.UTF8Bytes;
                eInFormEngine = EncodingForm.UTF8Bytes; // Linux
                if( m_nCodePage == CP_UTF16 )
                    eOutFormEngine = EncodingForm.UTF16;
            }
        }

        [CLSCompliant(false)]
        protected override unsafe void DoConvert
            (
            byte*       lpInBuffer,
            int         nInLen,
            byte*       lpOutBuffer,
            ref int     rnOutLen
            )
        {
            byte[] baIn = new byte[nInLen];
            ECNormalizeData.ByteStarToByteArr(lpInBuffer, nInLen, baIn);

            Encoding encFrom = Encoding.GetEncoding(m_nCodePage);
            Encoding encTo   = Encoding.Default;

            // Perform the conversion from one encoding to the other.
            System.Diagnostics.Debug.WriteLine("Starting with " + baIn.Length.ToString() + " bytes.");
            byte[] baOut = Encoding.Convert(encFrom, encTo, baIn);
            System.Diagnostics.Debug.WriteLine("Converted to " + baOut.Length.ToString() + " bytes.");

            if (baOut.Length > 0)
                rnOutLen = Marshal.SizeOf(baOut[0]) * baOut.Length;
            else
                rnOutLen = 0;
            //int size = rnOutLen + Marshal.SizeOf(baOut[0]);
            Marshal.Copy(baOut, 0, (IntPtr)lpOutBuffer, baOut.Length);
            Marshal.WriteByte((IntPtr)lpOutBuffer, rnOutLen, 0); // null terminate

/*
            if( m_bToWide )
            {
                rnOutLen = MultiByteToWideChar(m_nCodePage, 0, lpInBuffer, nInLen, (char*)lpOutBuffer, rnOutLen/2);
                rnOutLen *= 2;  // sizeof(WCHAR);	// size in bytes
            }
            else
            {
                rnOutLen = WideCharToMultiByte(m_nCodePage, 0, (char*)lpInBuffer, nInLen / 2, lpOutBuffer, rnOutLen, 0, 0);
            }
*/
        }

        protected override string   GetConfigTypeName
        {
            get { return typeof(CpEncConverterConfig).AssemblyQualifiedName; }
        }

        #endregion Abstract Base Class Overrides
    }
}