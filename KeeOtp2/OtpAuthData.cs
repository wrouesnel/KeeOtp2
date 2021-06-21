﻿using System;
using System.Collections.Generic;
using OtpNet;
using KeePass;
using KeePassLib.Security;
using KeePassLib.Utility;

namespace KeeOtp2
{
    /// <summary>
    /// Class that serializes and deserializes data into the Strings for the entry
    /// </summary>
    public class OtpAuthData : ICloneable
    {
        public ProtectedString Key { get; set; }
        public OtpType Type { get; set; }
        public OtpSecretEncoding Encoding { get; set; }
        public OtpHashMode Algorithm { get; set; }
        public int Period { get; set; }
        public int Digits { get; set; }
        public int Counter { get; set; }
        public bool KeeOtp1Mode { get; set; }
        public bool Proprietary { get; set; }

        public List<string> loadedFields { get; set; }

        public OtpAuthData()
        {
            this.Key = new ProtectedString(true, "");
            this.Type = OtpType.Totp;
            this.Encoding = OtpSecretEncoding.Base32;
            this.Algorithm = OtpHashMode.Sha1;

            this.Counter = 0;
            this.Period = 30;
            this.Digits = 6;

            this.KeeOtp1Mode = false;
            this.Proprietary = true;

            this.loadedFields = new List<string>();
        }
        
        public string GetPlainSecret()
        {
            if (!string.IsNullOrEmpty(this.Key.ReadString()))
            {
                if (this.Encoding == OtpSecretEncoding.Base32)
                    return Base32Encoding.ToString(this.Key.ReadUtf8());
                else if (this.Encoding == OtpSecretEncoding.Base64)
                    return Convert.ToBase64String(this.Key.ReadUtf8());
                else if (this.Encoding == OtpSecretEncoding.Hex)
                    return MemUtil.ByteArrayToHexString(this.Key.ReadUtf8());
                else if (this.Encoding == OtpSecretEncoding.UTF8)
                    return StrUtil.Utf8.GetString(this.Key.ReadUtf8());
                else
                    return null;
            }
            return null;
        }

        public void SetPlainSecret(string secret)
        {
            if (this.Encoding == OtpSecretEncoding.Base32)
                this.Key = new ProtectedString(true, MemUtil.ParseBase32(secret));
            else if (this.Encoding == OtpSecretEncoding.Base64)
                this.Key = new ProtectedString(true, Convert.FromBase64String(secret));
            else if (this.Encoding == OtpSecretEncoding.Hex)
                this.Key = new ProtectedString(true, MemUtil.HexStringToByteArray(secret));
            else if (this.Encoding == OtpSecretEncoding.UTF8)
                this.Key = new ProtectedString(true, StrUtil.Utf8.GetBytes(secret));
        }

        public bool isForcedKeeOtp1Mode()
        {
            if (this.Type == OtpType.Steam)
                return true;
            return false;
        }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
