using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace EdenPOS
{
    public sealed class DniLookupResult
    {
        public bool Success { get; set; }
        public string Dni { get; set; }
        public string FullName { get; set; }
        public string Source { get; set; }
        public string Message { get; set; }
    }

    public static class DniLookupService
    {
        public const string ProviderName = "ApiPeruDev (padrón reducido SUNAT)";
        public const string Endpoint = "https://apiperu.dev/api/dni";
        private static readonly HttpClient Client = CreateClient();
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        public static async Task<DniLookupResult> LookupAsync(string dni)
        {
            var normalized = Validation.NormalizeDni(dni);
            if (!Validation.IsValidDni(normalized))
                return Fail(normalized, "El DNI debe contener exactamente 8 dígitos.");

            var local = AppData.FindCustomer(normalized);
            if (local != null && Validation.IsValidPersonName(local.FullName))
                return new DniLookupResult { Success = true, Dni = normalized, FullName = local.FullName, Source = "Registro local", Message = "Cliente encontrado localmente." };

            var settings = AppData.Settings;
            if (!settings.DniLookupEnabled)
                return Fail(normalized, "La consulta en línea está desactivada. Puede escribir el nombre manualmente.");
            var token = AppData.UnprotectSecret(settings.DniApiTokenProtected);
            if (string.IsNullOrWhiteSpace(token))
                return Fail(normalized, "Configure el token de ApiPeruDev en Configuración.");

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
                {
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    request.Content = new StringContent(Json.Serialize(new Dictionary<string, string> { { "dni", normalized } }), Encoding.UTF8, "application/json");
                    using (var response = await Client.SendAsync(request).ConfigureAwait(true))
                    {
                        var content = await response.Content.ReadAsStringAsync().ConfigureAwait(true);
                        if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                            return Fail(normalized, "El token de consulta no es válido o no tiene permiso.");
                        if (!response.IsSuccessStatusCode)
                            return Fail(normalized, "El proveedor no respondió correctamente (HTTP " + (int)response.StatusCode + ").");
                        var parsed = ParseResponse(content, normalized);
                        if (parsed.Success) AppData.SaveCustomer(normalized, parsed.FullName, ProviderName);
                        return parsed;
                    }
                }
            }
            catch (TaskCanceledException)
            {
                return Fail(normalized, "La consulta tardó demasiado. Revise la conexión e inténtelo nuevamente.");
            }
            catch (HttpRequestException)
            {
                return Fail(normalized, "No se pudo conectar con el proveedor de DNI.");
            }
            catch (Exception)
            {
                return Fail(normalized, "Ocurrió un error al consultar el DNI.");
            }
        }

        public static DniLookupResult ParseResponse(string json, string requestedDni)
        {
            try
            {
                var root = Json.DeserializeObject(json) as Dictionary<string, object>;
                if (root == null || !root.ContainsKey("success") || !(root["success"] is bool) || !(bool)root["success"])
                    return Fail(requestedDni, ReadString(root, "message") ?? "El DNI no fue encontrado en las fuentes públicas.");
                var data = root.ContainsKey("data") ? root["data"] as Dictionary<string, object> : null;
                var fullName = ReadString(data, "nombre_completo");
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    var names = ReadString(data, "nombres");
                    var paternal = ReadString(data, "apellido_paterno");
                    var maternal = ReadString(data, "apellido_materno");
                    fullName = (paternal + " " + maternal + " " + names).Trim();
                }
                fullName = Validation.NormalizeName(fullName);
                if (!Validation.IsValidPersonName(fullName))
                    return Fail(requestedDni, "El proveedor respondió, pero no devolvió un nombre completo válido.");
                return new DniLookupResult
                {
                    Success = true,
                    Dni = Validation.NormalizeDni(requestedDni),
                    FullName = fullName,
                    Source = ProviderName,
                    Message = "Identidad encontrada. Verifique el documento físico del cliente."
                };
            }
            catch
            {
                return Fail(requestedDni, "La respuesta del proveedor no tiene el formato esperado.");
            }
        }

        private static string ReadString(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.ContainsKey(key) || data[key] == null) return null;
            return Convert.ToString(data[key]);
        }

        private static DniLookupResult Fail(string dni, string message)
        {
            return new DniLookupResult { Success = false, Dni = Validation.NormalizeDni(dni), Message = message, Source = "" };
        }

        private static HttpClient CreateClient()
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            return new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        }
    }
}
