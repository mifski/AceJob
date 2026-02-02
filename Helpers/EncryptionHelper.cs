using System.Security.Cryptography;
using System.Text;

namespace AceJob.Helpers
{
    /// <summary>
    /// Helper class for encrypting and decrypting sensitive data (NRIC)
  /// Usage: When you need to display or use decrypted NRIC
    /// </summary>
    public static class EncryptionHelper
    {
   /// <summary>
        /// Encrypt plain text using AES-256
        /// </summary>
     public static string Encrypt(string plainText, string key, string iv)
        {
    if (string.IsNullOrEmpty(plainText))
    return plainText;

            var keyBytes = Encoding.UTF8.GetBytes(key.PadRight(32).Substring(0, 32));
       var ivBytes = Encoding.UTF8.GetBytes(iv.PadRight(16).Substring(0, 16));

   using (var aes = Aes.Create())
            {
  aes.Key = keyBytes;
                aes.IV = ivBytes;
             var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

          using (var msEncrypt = new MemoryStream())
       {
      using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
   using (var swEncrypt = new StreamWriter(csEncrypt))
      {
      swEncrypt.Write(plainText);
          }
         return Convert.ToBase64String(msEncrypt.ToArray());
    }
            }
        }

        /// <summary>
        /// Decrypt cipher text using AES-256
        /// </summary>
     public static string Decrypt(string cipherText, string key, string iv)
        {
            if (string.IsNullOrEmpty(cipherText))
       return cipherText;

 var keyBytes = Encoding.UTF8.GetBytes(key.PadRight(32).Substring(0, 32));
    var ivBytes = Encoding.UTF8.GetBytes(iv.PadRight(16).Substring(0, 16));

  using (var aes = Aes.Create())
 {
                aes.Key = keyBytes;
     aes.IV = ivBytes;
    var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

          using (var msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText)))
                using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
         using (var srDecrypt = new StreamReader(csDecrypt))
              {
              return srDecrypt.ReadToEnd();
       }
            }
    }

        /// <summary>
      /// Mask NRIC for display (show only last 4 digits)
        /// Example: S1234567A ¡ú *****567A
     /// </summary>
  public static string MaskNRIC(string nric)
        {
   if (string.IsNullOrEmpty(nric) || nric.Length < 4)
         return nric;

return new string('*', nric.Length - 4) + nric.Substring(nric.Length - 4);
        }
    }
}
