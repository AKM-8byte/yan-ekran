using System.Security.Cryptography;
using System.Text;
using TelefonPaneli.Core.Settings;

namespace TelefonPaneli.Core.Security;

public enum PairStatus { Ok, WrongCode, Locked }

public sealed record PairResult(PairStatus Status, string? Token);

/// <summary>4 haneli eşleştirme kodu ve kalıcı cihaz token'ları. Token'lar diskte SHA-256 olarak saklanır.</summary>
public sealed class PairingService
{
    public const int MaxAttempts = 5;
    public static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(30);

    readonly SettingsStore _store;
    readonly Func<int> _rng;
    readonly TimeProvider _time;
    readonly object _gate = new();
    int _wrong;
    DateTimeOffset _lockedUntil = DateTimeOffset.MinValue;

    public PairingService(SettingsStore store, Func<int>? rng = null, TimeProvider? time = null)
    {
        _store = store;
        _rng = rng ?? (() => RandomNumberGenerator.GetInt32(0, 10000));
        _time = time ?? TimeProvider.System;
        CurrentCode = NewCode();
    }

    public string CurrentCode { get; private set; }
    public event Action<string>? CodeChanged;
    public event Action? DevicesForgotten;

    public PairResult TryPair(string? code, string? deviceName)
    {
        string? newCode = null;
        PairResult result;
        lock (_gate)
        {
            if (_time.GetUtcNow() < _lockedUntil) return new(PairStatus.Locked, null);

            if (!string.Equals(code?.Trim(), CurrentCode, StringComparison.Ordinal))
            {
                if (++_wrong >= MaxAttempts)
                {
                    _wrong = 0;
                    _lockedUntil = _time.GetUtcNow() + LockDuration;
                    newCode = CurrentCode = NewCode();
                    result = new(PairStatus.Locked, null);
                }
                else result = new(PairStatus.WrongCode, null);
            }
            else
            {
                _wrong = 0;
                var token = Base64Url(RandomNumberGenerator.GetBytes(32));
                var name = string.IsNullOrWhiteSpace(deviceName) ? "Telefon" : deviceName.Trim();
                if (name.Length > 40) name = name[..40];
                _store.Update(s => { s.Devices.Add(new DeviceToken(Guid.NewGuid().ToString("N"), name, Hash(token), _time.GetUtcNow())); return s; });
                newCode = CurrentCode = NewCode();
                result = new(PairStatus.Ok, token);
            }
        }
        if (newCode is not null) CodeChanged?.Invoke(newCode);
        return result;
    }

    public bool IsValid(string? token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        var h = Hash(token);
        return _store.Load().Devices.Any(d => CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(d.TokenHash), Encoding.ASCII.GetBytes(h)));
    }

    public void ForgetAll()
    {
        lock (_gate)
        {
            _store.Update(s => { s.Devices.Clear(); return s; });
        }
        DevicesForgotten?.Invoke();
    }

    string NewCode() => (_rng() % 10000).ToString("D4");

    static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    static string Base64Url(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
