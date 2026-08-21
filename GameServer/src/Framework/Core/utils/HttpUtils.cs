using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.IO;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Framework.Core.Utils {
    public class HttpUtils
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        public static async Task<long> HttpPost(string url, long openId)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Accept.Clear();
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));

                byte[] openIdBody = new byte[8];
                UtilsHelper.WriteULongLE(openIdBody, 0, (ulong)openId);
                var content = new ByteArrayContent(openIdBody);
                HttpResponseMessage response = await _httpClient.PostAsync(url, content);
                response.EnsureSuccessStatusCode(); // 确保响应成功

                byte[] responseBody = await response.Content.ReadAsByteArrayAsync();
                return (long)UtilsHelper.ReadLongLE(responseBody, 0);
            }
            catch (Exception e) {
                
            }
            /*finally
            {
                _httpClient.Dispose();
            }*/
            return 0;
        }
    }
}