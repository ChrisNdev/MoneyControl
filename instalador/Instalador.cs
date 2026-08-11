// Instalador do MoneyControl.
//
// Um executável só, que carrega o app dentro de si como recurso e serve também de
// desinstalador (`--desinstalar`). Reusa Ui.cs: os mesmos tokens, a mesma marca e os mesmos
// glifos da aplicação, então o instalador parece a aplicação em vez de parecer um assistente
// genérico de 1998.
//
// Instala em %LOCALAPPDATA%\Programs\MoneyControl e registra em HKCU. Nada disso pede
// elevação -- e o instalador que não abre escudo do UAC é o que dá para deixar bonito.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MoneyControl {

public static class Instalador {
    public const string NOME = "MoneyControl";
    public const string VERSAO = "5.0";
    public const string CHAVE = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\MoneyControl";

    public static string Destino {
        get {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                @"Programs\MoneyControl");
        }
    }

    [STAThread]
    static void Main(string[] args) {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        bool tirando = args.Length > 0 && args[0] == "--desinstalar";
        Application.Run(new Tela(tirando));
    }
}

public class Tela : Form {
    const int L = 760, A = 480, ESQ = 300;   // largura, altura, faixa da marca

    readonly bool tirando;
    string fase = "pronto";                  // pronto | indo | fim | erro
    string recado = "";
    double pct;
    bool atalho = true;                      // criar atalho na área de trabalho
    Rectangle caixaAtalho, caixaFechar;
    Botao acao;

    public Tela(bool tirando) {
        this.tirando = tirando;
        FormBorderStyle = FormBorderStyle.None;
        ClientSize = new Size(L, A);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Ui.BG;
        Text = (tirando ? "Desinstalar " : "Instalar ") + Instalador.NOME;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);

        caixaFechar = new Rectangle(L - 44, 16, 28, 28);
        caixaAtalho = new Rectangle(ESQ + 48, 300, 22, 22);

        // destrutivo é secundário com texto vermelho, como o resto do app faz: a cor de
        // convite fica reservada para o caminho que a pessoa veio seguir
        acao = new Botao(tirando ? "Desinstalar" : "Instalar", tirando ? "excluir" : "backup", !tirando);
        acao.Perigo = tirando;
        acao.Location = new Point(ESQ + 48, 356);
        acao.Height = 46;
        acao.Medir();
        acao.Click += (s, e) => Agir();
        Controls.Add(acao);
    }

    /// <summary>O botão é um só; o que ele faz depende de onde a instalação está.</summary>
    void Agir() {
        if (fase == "pronto") { Executar(); return; }
        if (fase == "fim" && !tirando) {
            try { Process.Start(Path.Combine(Instalador.Destino, "MoneyControl.exe")); } catch { }
        }
        Close();
    }

    /* ---------------------------- moldura ---------------------------- */

    protected override CreateParams CreateParams {
        get {
            var p = base.CreateParams;
            p.ClassStyle |= 0x20000;   // CS_DROPSHADOW: sombra, já que não há moldura do sistema
            return p;
        }
    }

    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, int wp, int lp);

    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        if (caixaFechar.Contains(e.Location)) { Close(); return; }
        if (fase == "pronto" && caixaAtalho.Contains(Alvo(e.Location))) {
            atalho = !atalho; Invalidate(); return;
        }
        // sem barra de título do sistema, arrastar é por conta da janela
        if (e.Button == MouseButtons.Left && e.Y < 80) {
            ReleaseCapture();
            SendMessage(Handle, 0xA1, 2, 0);   // WM_NCLBUTTONDOWN / HTCAPTION
        }
    }

    // a caixa do atalho tem alvo maior que o desenho: 22px é pouco para acertar de mouse
    Point Alvo(Point p) { return new Point(p.X + 6, p.Y + 6); }

    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);
        bool mao = caixaFechar.Contains(e.Location) ||
                   (fase == "pronto" && caixaAtalho.Contains(Alvo(e.Location)));
        Cursor = mao ? Cursors.Hand : Cursors.Default;
    }

    /* ---------------------------- desenho ---------------------------- */

    protected override void OnPaint(PaintEventArgs e) {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Ui.BG);

        Faixa(g);
        using (var caneta = new Pen(Ui.LINE)) g.DrawLine(caneta, ESQ, 0, ESQ, A);

        int x = ESQ + 48;
        Ui.Icone(g, "fechar", new RectangleF(caixaFechar.X + 7, caixaFechar.Y + 7, 14, 14), Ui.FG3);

        if (fase == "fim") { Fim(g, x); return; }
        if (fase == "erro") { Erro(g, x); return; }

        Ui.Txt(g, tirando ? "Desinstalar" : "Instalar", Ui.F(30, true), Ui.FG,
               new Rectangle(x, 64, L - x - 40, 44));
        Ui.TxtQuebra(g, tirando
                ? "O aplicativo sai do computador. Seus lançamentos, o cofre e os backups ficam onde estão."
                : "Sem assistente, sem escolher pasta, sem pedir permissão de administrador. Um clique e acabou.",
            Ui.F(14), Ui.FG2, new Rectangle(x, 116, L - x - 64, 60));

        Ui.Rotulo(g, tirando ? "SAI DE" : "VAI PARA", Ui.LBL, x, 196);
        Ui.Txt(g, Encurtar(Instalador.Destino), Ui.F(13), Ui.FG3, new Rectangle(x, 216, L - x - 48, 22));

        if (!tirando) {
            Caixa(g, caixaAtalho, atalho);
            Ui.Txt(g, "Criar atalho na área de trabalho", Ui.F(14), Ui.FG2,
                   new Rectangle(caixaAtalho.Right + 14, caixaAtalho.Y + 1, 300, 22));
        }

        if (fase == "indo") {
            acao.Visible = false;
            Ui.Progresso(g, new Rectangle(x, 372, L - x - 48, 8), pct, Ui.ACC);
            Ui.Txt(g, recado, Ui.F(13), Ui.FG3, new Rectangle(x, 390, L - x - 48, 22));
        }
        Rodape(g, x);
    }

    void Rodape(Graphics g, int x) {
        Ui.Divisoria(g, x, A - 56, L - x - 48);
        Ui.Txt(g, Peso() + " · sem dependência · MIT", Ui.F(12), Ui.LBL,
               new Rectangle(x, A - 42, L - x - 48, 20));
    }

    /// <summary>O tamanho do app que está embutido aqui dentro, não o do instalador.</summary>
    static string Peso() {
        try {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("app"))
                if (s != null) return (s.Length / 1024) + " KB";
        } catch { }
        return "leve";
    }

    /// <summary>A faixa da marca: o mesmo cartão holográfico que o app põe no topo do menu.</summary>
    void Faixa(Graphics g) {
        using (var b = new LinearGradientBrush(new Rectangle(0, 0, ESQ, A),
                                               Ui.H("#14171A"), Ui.H("#0B0C0E"), 70f))
            g.FillRectangle(b, 0, 0, ESQ, A);

        Ui.Marca(g, new RectangleF(ESQ / 2f - 60, 118, 120, 120));
        Ui.TxtCentro(g, Instalador.NOME, Ui.F(24, true), Ui.FG, new Rectangle(0, 262, ESQ, 32));
        Ui.TxtCentro(g, "v" + Instalador.VERSAO, Ui.F(13), Ui.FG3, new Rectangle(0, 294, ESQ, 20));
        Ui.TxtCentro(g, "Cartão compartilhado e dívidas", Ui.F(12), Ui.LBL,
                     new Rectangle(0, 330, ESQ, 20));
    }

    void Caixa(Graphics g, Rectangle r, bool marcado) {
        Ui.Fill(g, r, 6, marcado ? Ui.ACC : Ui.FIELD);
        if (!marcado) Ui.Borda(g, r, 6, Ui.LINE2);
        else Ui.Icone(g, "check", new RectangleF(r.X + 4, r.Y + 4, 14, 14), Ui.ONACC);
    }

    void Fim(Graphics g, int x) {
        acao.Visible = true;
        Ui.Chip(g, new Rectangle(x, 64, 56, 56), "check", Ui.OK, Ui.OKBG, 18);
        Ui.Txt(g, tirando ? "Desinstalado" : "Pronto", Ui.F(30, true), Ui.FG,
               new Rectangle(x, 140, L - x - 40, 44));
        Ui.TxtQuebra(g, recado, Ui.F(14), Ui.FG2, new Rectangle(x, 192, L - x - 64, 60));
    }

    void Erro(Graphics g, int x) {
        acao.Visible = true;
        Ui.Chip(g, new Rectangle(x, 64, 56, 56), "alerta", Ui.BAD, Ui.BADBG, 18);
        Ui.Txt(g, "Não deu", Ui.F(30, true), Ui.FG, new Rectangle(x, 140, L - x - 40, 44));
        Ui.TxtQuebra(g, recado, Ui.F(13), Ui.FG2, new Rectangle(x, 192, L - x - 64, 120));
    }

    static string Encurtar(string caminho) {
        string casa = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return caminho.StartsWith(casa, StringComparison.OrdinalIgnoreCase)
             ? "%USERPROFILE%" + caminho.Substring(casa.Length) : caminho;
    }

    /* ---------------------------- o trabalho ---------------------------- */

    void Passo(string txt, double p) {
        recado = txt; pct = p;
        Invalidate(); Update();
        // o copiar leva milissegundos; sem isto a barra pula de 0 a 100 e ninguém lê nada
        var ate = DateTime.Now.AddMilliseconds(260);
        while (DateTime.Now < ate) { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
    }

    void Executar() {
        fase = "indo"; acao.Visible = false; Invalidate();
        try {
            if (tirando) Desinstalar(); else Instalar();
            fase = "fim";
        } catch (Exception e) {
            fase = "erro";
            recado = e.Message;
        }
        bool abrir = fase == "fim" && !tirando;
        acao.Icone = abrir ? "seta" : null;
        acao.Text = abrir ? "Abrir o MoneyControl" : "Fechar";   // o setter de Text remede a largura
        acao.Medir();
        acao.Visible = true;
        Invalidate();
    }

    void Instalar() {
        string dir = Instalador.Destino;
        string exe = Path.Combine(dir, "MoneyControl.exe");

        Passo("Preparando a pasta", .12);
        Directory.CreateDirectory(dir);

        Passo("Copiando o aplicativo", .38);
        using (var dentro = Assembly.GetExecutingAssembly().GetManifestResourceStream("app")) {
            if (dentro == null) throw new Exception("O instalador veio sem o aplicativo dentro.");
            using (var fs = File.Create(exe)) dentro.CopyTo(fs);
        }

        Passo("Deixando o desinstalador", .58);
        File.Copy(Assembly.GetExecutingAssembly().Location,
                  Path.Combine(dir, "desinstalar.exe"), true);

        Passo("Criando os atalhos", .78);
        Atalho(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                            "MoneyControl.lnk"), exe, dir);
        if (atalho)
            Atalho(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                                "MoneyControl.lnk"), exe, dir);

        Passo("Registrando", .94);
        using (var k = Registry.CurrentUser.CreateSubKey(Instalador.CHAVE)) {
            k.SetValue("DisplayName", Instalador.NOME);
            k.SetValue("DisplayVersion", Instalador.VERSAO);
            k.SetValue("Publisher", "ChrisNdev");
            k.SetValue("InstallLocation", dir);
            k.SetValue("DisplayIcon", exe);
            k.SetValue("UninstallString", "\"" + Path.Combine(dir, "desinstalar.exe") + "\" --desinstalar");
            k.SetValue("EstimatedSize", (int)(new FileInfo(exe).Length / 1024), RegistryValueKind.DWord);
            k.SetValue("NoModify", 1, RegistryValueKind.DWord);
            k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }

        Passo("Pronto", 1);
        recado = "O MoneyControl está no menu Iniciar" +
                 (atalho ? " e na área de trabalho." : ".") +
                 " Seus dados ficam em %LOCALAPPDATA%\\MoneyControl.";
    }

    void Desinstalar() {
        string dir = Instalador.Destino;

        Passo("Tirando os atalhos", .3);
        Apagar(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "MoneyControl.lnk"));
        Apagar(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "MoneyControl.lnk"));

        Passo("Limpando o registro", .6);
        try { Registry.CurrentUser.DeleteSubKeyTree(Instalador.CHAVE, false); } catch { }

        Passo("Removendo o aplicativo", .9);
        Apagar(Path.Combine(dir, "MoneyControl.exe"));

        // o desinstalador não se apaga rodando: quem varre a pasta é um cmd solto,
        // depois que este processo já saiu
        Process.Start(new ProcessStartInfo("cmd.exe",
                "/c ping -n 3 127.0.0.1 >nul & rmdir /s /q \"" + dir + "\"") {
            CreateNoWindow = true, UseShellExecute = false,
        });

        Passo("Pronto", 1);
        recado = "O MoneyControl saiu. Seus lançamentos e backups continuam em " +
                 "%LOCALAPPDATA%\\MoneyControl — apague essa pasta à mão se quiser sumir com tudo.";
    }

    static void Apagar(string caminho) { try { if (File.Exists(caminho)) File.Delete(caminho); } catch { } }

    /// <summary>
    /// Atalho pelo Windows Script Host, por ligação tardia: escrever .lnk na mão é formato
    /// binário documentado a duras penas, e referenciar a COM do WSH puxaria interop.
    /// </summary>
    static void Atalho(string lnk, string alvo, string pasta) {
        var t = Type.GetTypeFromProgID("WScript.Shell");
        if (t == null) return;
        object sh = Activator.CreateInstance(t);
        object a = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
        var ta = a.GetType();
        ta.InvokeMember("TargetPath", BindingFlags.SetProperty, null, a, new object[] { alvo });
        ta.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, a, new object[] { pasta });
        ta.InvokeMember("IconLocation", BindingFlags.SetProperty, null, a, new object[] { alvo + ",0" });
        ta.InvokeMember("Description", BindingFlags.SetProperty, null, a,
                        new object[] { "Cartão compartilhado e dívidas pessoais" });
        ta.InvokeMember("Save", BindingFlags.InvokeMethod, null, a, null);
    }
}

}
