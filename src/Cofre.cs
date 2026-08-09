// Persistência criptografada.
//
// No computador: o arquivo inteiro é protegido pelo DPAPI do Windows (escopo CurrentUser).
// O Windows guarda o segredo, derivado das credenciais da conta — não há chave em disco
// para alguém copiar, e o arquivo levado para outra máquina não abre.
//
// No backup: DPAPI não serve, porque um backup que só abre neste Windows é inútil
// justamente no dia em que o PC morre. Então vai com senha escolhida na hora:
// PBKDF2-SHA256 -> AES-256-CBC, autenticado com HMAC-SHA256 (encrypt-then-MAC).
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace MoneyControl {

public static class Cofre {
    public const int ITER = 310000;              // PBKDF2 (recomendação OWASP)
    static readonly byte[] MARCA = Encoding.ASCII.GetBytes("MCB1");

    public static string Pasta {
        get {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MoneyControl");
        }
    }
    public static string Arquivo { get { return Path.Combine(Pasta, "dados.bin"); } }

    static JavaScriptSerializer Js() {
        var js = new JavaScriptSerializer();
        js.MaxJsonLength = int.MaxValue;
        return js;
    }

    public static string ParaJson(Estado s) { return Js().Serialize(s); }

    /// <summary>Aceita JSON torto sem derrubar o programa: o que falta vira padrão.</summary>
    public static Estado DeJson(string json) {
        var s = Js().Deserialize<Estado>(json);
        if (s == null) s = new Estado();
        if (s.people == null) s.people = new System.Collections.Generic.List<Pessoa>();
        if (s.buys == null) s.buys = new System.Collections.Generic.List<Lanc>();
        if (s.debts == null) s.debts = new System.Collections.Generic.List<Lanc>();
        if (s.paid == null) s.paid = new System.Collections.Generic.Dictionary<string, string>();
        if (s.dpaid == null) s.dpaid = new System.Collections.Generic.Dictionary<string, string>();
        if (s.cfg == null) s.cfg = new Cfg();
        if (s.cfg.due < 1 || s.cfg.due > 28) s.cfg.due = 10;
        return s;
    }

    /* ---------------------------- disco ---------------------------- */

    public static Estado Carregar() {
        if (!File.Exists(Arquivo)) return new Estado();
        byte[] claro = ProtectedData.Unprotect(File.ReadAllBytes(Arquivo), null, DataProtectionScope.CurrentUser);
        return DeJson(Encoding.UTF8.GetString(claro));
    }

    /// <summary>
    /// Grava em arquivo temporário e só então substitui o bom: uma queda de energia
    /// no meio da escrita não pode deixar o usuário sem dados nenhum.
    /// </summary>
    public static void Salvar(Estado s) {
        Directory.CreateDirectory(Pasta);
        byte[] cifrado = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(ParaJson(s)), null, DataProtectionScope.CurrentUser);
        string tmp = Arquivo + ".tmp";
        File.WriteAllBytes(tmp, cifrado);
        if (File.Exists(Arquivo)) File.Replace(tmp, Arquivo, null);
        else File.Move(tmp, Arquivo);
    }

    /* ------------------------ backup com senha ------------------------ */

    // formato: "MCB1" | salt(16) | iv(16) | hmac(32) | cifrado
    public static byte[] SelarComSenha(Estado s, string senha) {
        var salt = Aleatorio(16);
        byte[] kc, km;
        Derivar(senha, salt, out kc, out km);

        using (var aes = new AesManaged { KeySize = 256, Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7 }) {
            aes.Key = kc;
            aes.GenerateIV();
            byte[] cifrado;
            using (var enc = aes.CreateEncryptor())
                cifrado = Transformar(enc, Encoding.UTF8.GetBytes(ParaJson(s)));

            // encrypt-then-MAC: autentica salt+iv+cifrado, então adulterar qualquer parte falha
            byte[] assinado = Concat(salt, aes.IV, cifrado);
            byte[] mac;
            using (var h = new HMACSHA256(km)) mac = h.ComputeHash(assinado);

            return Concat(MARCA, salt, aes.IV, mac, cifrado);
        }
    }

    public static Estado AbrirComSenha(byte[] arq, string senha) {
        if (arq.Length < 4 + 16 + 16 + 32 || !arq.Take(4).SequenceEqual(MARCA))
            throw new InvalidDataException("Este arquivo não é um backup do MoneyControl.");

        var salt = Fatia(arq, 4, 16);
        var iv = Fatia(arq, 20, 16);
        var mac = Fatia(arq, 36, 32);
        var cifrado = Fatia(arq, 68, arq.Length - 68);

        byte[] kc, km;
        Derivar(senha, salt, out kc, out km);

        byte[] esperado;
        using (var h = new HMACSHA256(km)) esperado = h.ComputeHash(Concat(salt, iv, cifrado));
        // comparação em tempo constante: não entrega o MAC byte a byte
        if (!IguaisEmTempoConstante(mac, esperado))
            throw new CryptographicException("Senha incorreta ou arquivo corrompido.");

        using (var aes = new AesManaged { KeySize = 256, Mode = CipherMode.CBC, Padding = PaddingMode.PKCS7 }) {
            aes.Key = kc;
            aes.IV = iv;
            using (var dec = aes.CreateDecryptor())
                return DeJson(Encoding.UTF8.GetString(Transformar(dec, cifrado)));
        }
    }

    /// <summary>Uma senha, duas chaves independentes: uma cifra, outra autentica.</summary>
    static void Derivar(string senha, byte[] salt, out byte[] chaveCifra, out byte[] chaveMac) {
        using (var kdf = new Rfc2898DeriveBytes(senha ?? "", salt, ITER)) {
            chaveCifra = kdf.GetBytes(32);
            chaveMac = kdf.GetBytes(32);
        }
    }

    static bool IguaisEmTempoConstante(byte[] a, byte[] b) {
        if (a.Length != b.Length) return false;
        int dif = 0;
        for (int i = 0; i < a.Length; i++) dif |= a[i] ^ b[i];
        return dif == 0;
    }

    static byte[] Transformar(ICryptoTransform t, byte[] dados) {
        using (var ms = new MemoryStream()) {
            using (var cs = new CryptoStream(ms, t, CryptoStreamMode.Write)) cs.Write(dados, 0, dados.Length);
            return ms.ToArray();
        }
    }

    public static byte[] Aleatorio(int n) {
        var b = new byte[n];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(b);
        return b;
    }

    static byte[] Concat(params byte[][] partes) {
        var outp = new byte[partes.Sum(p => p.Length)];
        int o = 0;
        foreach (var p in partes) { Buffer.BlockCopy(p, 0, outp, o, p.Length); o += p.Length; }
        return outp;
    }

    static byte[] Fatia(byte[] b, int inicio, int tam) {
        var outp = new byte[tam];
        Buffer.BlockCopy(b, inicio, outp, 0, tam);
        return outp;
    }
}

}
