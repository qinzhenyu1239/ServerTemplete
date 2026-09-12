using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Framework.Core.Idempotency
{
    public static class OperationIdValidator
    {
        public static bool IsValid(string operationId)
        {
            if (operationId == null || operationId.Length != 32) return false;
            for (int index = 0; index < operationId.Length; index++)
            {
                char value = operationId[index];
                if (!((value >= '0' && value <= '9') || (value >= 'a' && value <= 'f')))
                    return false;
            }
            return Guid.TryParseExact(operationId, "N", out Guid parsed) && parsed != Guid.Empty;
        }
    }

    public static class OperationRequestHasher
    {
        public static string Compute(params object[] parts)
        {
            string canonical = string.Join("|", parts ?? Array.Empty<object>());
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        }
    }

    public static class EconomicOperationPersistence
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

        public static T Deserialize<T>(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("The idempotent operation has no stored response.");
            return JsonSerializer.Deserialize<T>(value, JsonOptions) ??
                   throw new InvalidOperationException("Failed to restore the idempotent operation response.");
        }
    }

    public sealed class IdempotentOperationConflictException : Exception
    {
        public IdempotentOperationConflictException(string operationId)
            : base($"Operation ID '{operationId}' was reused with different parameters.") { }
    }
}
