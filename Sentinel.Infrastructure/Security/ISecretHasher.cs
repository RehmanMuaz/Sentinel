namespace Sentinel.Infrastructure.Security;

public interface ISecretHasher
{
    string Hash(string secret);
    bool Verify(string secret, string hash);
}
