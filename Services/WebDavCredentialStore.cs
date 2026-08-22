using System.Security.Cryptography;
using System.Text;
using Windows.Security.Credentials;

namespace PageArc.Services;

public sealed class WebDavCredentialStore
{
    private const string ResourcePrefix = "PageArc.WebDAV.";
    private readonly PasswordVault _vault = new();

    public void Save(string endpoint, string username, string password)
    {
        if (string.IsNullOrWhiteSpace(password)) return;
        Remove(endpoint, username);
        _vault.Add(new PasswordCredential(Resource(endpoint), username, password));
    }

    public string? Read(string endpoint, string username)
    {
        try
        {
            var credential = _vault.Retrieve(Resource(endpoint), username);
            credential.RetrievePassword();
            return credential.Password;
        }
        catch
        {
            return null;
        }
    }

    private void Remove(string endpoint, string username)
    {
        try { _vault.Remove(_vault.Retrieve(Resource(endpoint), username)); }
        catch { }
    }

    private static string Resource(string endpoint)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(endpoint.Trim().ToUpperInvariant()));
        return ResourcePrefix + Convert.ToHexString(hash);
    }
}
