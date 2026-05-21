using System.Security.Cryptography;
using System.Text;

static string ToPem(string label, byte[] data)
{
    string base64 = Convert.ToBase64String(data);
    var builder = new StringBuilder();

    builder.AppendLine($"-----BEGIN {label}-----");

    for (int i = 0; i < base64.Length; i += 64)
    {
        int length = Math.Min(64, base64.Length - i);
        builder.AppendLine(base64.Substring(i, length));
    }

    builder.AppendLine($"-----END {label}-----");

    return builder.ToString();
}

using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

byte[] privateKey = ecdsa.ExportPkcs8PrivateKey();
byte[] publicKey = ecdsa.ExportSubjectPublicKeyInfo();

string privatePem = ToPem("PRIVATE KEY", privateKey);
string publicPem = ToPem("PUBLIC KEY", publicKey);

File.WriteAllText("backend-signing-private.pem", privatePem);
File.WriteAllText("backend-signing-public.pem", publicPem);

Console.WriteLine("Generated:");
Console.WriteLine(Path.GetFullPath("backend-signing-private.pem"));
Console.WriteLine(Path.GetFullPath("backend-signing-public.pem"));