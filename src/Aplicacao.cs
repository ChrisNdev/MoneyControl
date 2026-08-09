// Ponto de entrada, paleta e as peças de UI que as telas reaproveitam.
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace CardControl {

public static class Aplicacao {
    public const string VERSAO = "4.0";

    public static string Exe { get { return Assembly.GetExecutingAssembly().Location; } }

    /* paleta escura */
    public static readonly Color BG      = ColorTranslator.FromHtml("#08090d");
    public static readonly Color SURFACE = ColorTranslator.FromHtml("#101219");
    public static readonly Color SURF2   = ColorTranslator.FromHtml("#14161f");
    public static readonly Color LINE    = ColorTranslator.FromHtml("#20232e");
    public static readonly Color SEL     = ColorTranslator.FromHtml("#1a1a2e");
    public static readonly Color FG      = ColorTranslator.FromHtml("#e8eaf0");
    public static readonly Color MUTED   = ColorTranslator.FromHtml("#868da0");
    public static readonly Color ACCENT  = ColorTranslator.FromHtml("#7c7cf5");
    public static readonly Color OK      = ColorTranslator.FromHtml("#10b981");
    public static readonly Color WARN    = ColorTranslator.FromHtml("#f59e0b");
    public static readonly Color BAD     = ColorTranslator.FromHtml("#ef4444");

    [STAThread]
    static int Main(string[] args) {
        if (args.Length > 0 && args[0] == "--test") return Testes.Rodar();

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Estado s;
        try {
            s = Cofre.Carregar();
        } catch (Exception e) {
            // Arquivo ilegível não pode virar arquivo apagado: nada é gravado por cima
            // enquanto o usuário não escolher restaurar um backup.
            if (!Confirma("Não consegui abrir seus dados.\n\n" + e.Message +
                          "\n\nSeu arquivo atual foi mantido intacto.\nQuer restaurar um backup agora?"))
                return 1;
            s = RestaurarDeArquivo();
            if (s == null) return 1;
            try { Cofre.Salvar(s); } catch (Exception e2) { Erro("Não deu pra gravar: " + e2.Message); return 1; }
        }

        Application.Run(new Janela(s));
        return 0;
    }

    /* ---------------------------- avisos ---------------------------- */

    public static void Erro(string msg) {
        MessageBox.Show(msg, "CardControl", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    public static void Aviso(string msg) {
        MessageBox.Show(msg, "CardControl", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public static bool Confirma(string msg) {
        return MessageBox.Show(msg, "CardControl", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
               == DialogResult.Yes;
    }

    /// <summary>Caixa de senha. Devolve null se o usuário desistir.</summary>
    public static string PedirSenha(string titulo, string texto) {
        using (var f = new Form {
            Text = titulo, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false,
            MinimizeBox = false, StartPosition = FormStartPosition.CenterParent,
            ClientSize = new Size(400, 150), BackColor = BG, ForeColor = FG, Font = new Font("Segoe UI", 9f),
        }) {
            var lbl = new Label { Text = texto, Location = new Point(16, 14), Size = new Size(368, 44), ForeColor = MUTED };
            var txt = new TextBox {
                UseSystemPasswordChar = true, Location = new Point(16, 64), Size = new Size(368, 24),
                BackColor = SURF2, ForeColor = FG, BorderStyle = BorderStyle.FixedSingle,
            };
            var ok = Btn("OK", null, true);
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(284, 102); ok.Size = new Size(100, 30);
            var cancel = Btn("Cancelar", null);
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(176, 102); cancel.Size = new Size(100, 30);

            f.Controls.AddRange(new Control[] { lbl, txt, ok, cancel });
            f.AcceptButton = ok; f.CancelButton = cancel;
            return f.ShowDialog() == DialogResult.OK ? txt.Text : null;
        }
    }

    /* ---------------------------- backup ---------------------------- */

    /// <summary>
    /// Abre um backup do disco: `.ccb` protegido por senha ou `.json` em texto puro.
    /// Devolve null se o usuário desistir ou se o arquivo não servir.
    /// </summary>
    public static Estado RestaurarDeArquivo() {
        using (var dlg = new OpenFileDialog {
            Title = "Restaurar backup",
            Filter = "Backup do CardControl (*.ccb;*.json)|*.ccb;*.json|Todos os arquivos|*.*",
        }) {
            if (dlg.ShowDialog() != DialogResult.OK) return null;
            try {
                byte[] bruto = File.ReadAllBytes(dlg.FileName);
                if (Path.GetExtension(dlg.FileName).Equals(".json", StringComparison.OrdinalIgnoreCase)) {
                    var claro = Cofre.DeJson(Encoding.UTF8.GetString(bruto));
                    if (!Confirma("Isso substitui TODOS os dados atuais. Continuar?")) return null;
                    return claro;
                }
                string senha = PedirSenha("Restaurar backup", "Senha deste backup:");
                if (senha == null) return null;
                var dados = Cofre.AbrirComSenha(bruto, senha);   // erra a senha -> exceção, nunca lixo silencioso
                if (!Confirma("Isso substitui TODOS os dados atuais. Continuar?")) return null;
                return dados;
            } catch (Exception e) {
                Erro("Não deu pra importar: " + e.Message);
                return null;
            }
        }
    }

    /// <summary>Grava um `.ccb` com senha escolhida na hora — é o backup que abre em qualquer máquina.</summary>
    public static void ExportarBackup(Estado s) {
        string senha = PedirSenha("Backup criptografado",
            "Crie uma senha para este backup.\nÉ ela que vai abrir o arquivo em outro computador:");
        if (senha == null) return;
        if (senha.Length < 8) { Erro("Use pelo menos 8 caracteres."); return; }
        if (senha != PedirSenha("Backup criptografado", "Repita a senha do backup:")) {
            Erro("As senhas não bateram."); return;
        }
        using (var dlg = new SaveFileDialog {
            Title = "Salvar backup", Filter = "Backup do CardControl (*.ccb)|*.ccb",
            FileName = "cardcontrol-" + Calc.Hoje() + ".ccb",
        }) {
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try {
                File.WriteAllBytes(dlg.FileName, Cofre.SelarComSenha(s, senha));
                Aviso("Backup salvo.\n\nGuarde a senha: sem ela o arquivo não abre, e não há recuperação.");
            } catch (Exception e) { Erro("Não deu pra exportar: " + e.Message); }
        }
    }

    public static void ExportarPlano(Estado s) {
        if (!Confirma("Este arquivo sai SEM criptografia — qualquer um que abrir vê tudo.\n\nContinuar?")) return;
        using (var dlg = new SaveFileDialog {
            Title = "Exportar sem criptografia", Filter = "JSON (*.json)|*.json",
            FileName = "cardcontrol-" + Calc.Hoje() + "-SEM-SENHA.json",
        }) {
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try { File.WriteAllText(dlg.FileName, Cofre.ParaJson(s), Encoding.UTF8); }
            catch (Exception e) { Erro("Não deu pra exportar: " + e.Message); }
        }
    }

    /* ---------------------------- peças de UI ---------------------------- */

    public static Button Btn(string txt, EventHandler ev, bool primario = false) {
        var b = new Button {
            Text = txt, AutoSize = false, Height = 30, FlatStyle = FlatStyle.Flat,
            BackColor = primario ? ACCENT : SURFACE, ForeColor = primario ? Color.White : FG,
            Font = new Font("Segoe UI", 9f, primario ? FontStyle.Bold : FontStyle.Regular),
            Padding = new Padding(6, 0, 6, 0), UseVisualStyleBackColor = false, Cursor = Cursors.Hand,
        };
        b.FlatAppearance.BorderColor = primario ? ACCENT : LINE;
        b.Width = Math.Max(84, TextRenderer.MeasureText(txt, b.Font).Width + 26);
        if (ev != null) b.Click += ev;
        return b;
    }

    public static Label Txt(string s, float tam = 9f, Color? cor = null, bool negrito = false) {
        return new Label {
            Text = s, AutoSize = true, ForeColor = cor ?? FG,
            Font = new Font("Segoe UI", tam, negrito ? FontStyle.Bold : FontStyle.Regular),
            Margin = new Padding(0, 2, 0, 2),
        };
    }

    /// <summary>Cartão de número grande do topo das telas de resumo.</summary>
    public static Panel Stat(string titulo, string valor, string rodape, Color cor) {
        var p = new Panel { BackColor = SURFACE, Padding = new Padding(12, 10, 12, 10), Margin = new Padding(0, 0, 10, 0) };
        // o Fill entra primeiro de propósito: docking é resolvido do último controle para o
        // primeiro, então quem preenche tem que ser o último da fila ou come o rodapé
        p.Controls.Add(new Label { Text = valor, Dock = DockStyle.Fill, ForeColor = cor,
                                   Font = new Font("Segoe UI", 14f, FontStyle.Bold), AutoEllipsis = true });
        p.Controls.Add(new Label { Text = rodape, Dock = DockStyle.Bottom, ForeColor = MUTED,
                                   Font = new Font("Segoe UI", 7.5f), Height = 16 });
        p.Controls.Add(new Label { Text = titulo, Dock = DockStyle.Top, ForeColor = MUTED,
                                   Font = new Font("Segoe UI", 7.5f), Height = 16 });
        return p;
    }

    public const int ALTURA_STATS = 96;

    /// <summary>Faixa de cartões de número, distribuída em colunas iguais.</summary>
    public static Control FaixaStats(params Panel[] cards) {
        var t = new TableLayoutPanel {
            ColumnCount = cards.Length, RowCount = 1, Height = ALTURA_STATS, BackColor = Color.Transparent,
        };
        for (int i = 0; i < cards.Length; i++) {
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cards.Length));
            cards[i].Dock = DockStyle.Fill;
            t.Controls.Add(cards[i], i, 0);
        }
        return t;
    }

    public static DataGridView Grade() {
        var g = new DataGridView {
            BackgroundColor = SURFACE, BorderStyle = BorderStyle.None, GridColor = LINE,
            AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
            ReadOnly = true, RowHeadersVisible = false, MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false, ColumnHeadersHeight = 30,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
        };
        g.ColumnHeadersDefaultCellStyle.BackColor = SURF2;
        g.ColumnHeadersDefaultCellStyle.ForeColor = MUTED;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = SURF2;
        g.ColumnHeadersDefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
        g.DefaultCellStyle.BackColor = SURFACE;
        g.DefaultCellStyle.ForeColor = FG;
        g.DefaultCellStyle.SelectionBackColor = SEL;
        g.DefaultCellStyle.SelectionForeColor = FG;
        g.DefaultCellStyle.Padding = new Padding(6, 0, 6, 0);
        g.RowTemplate.Height = 30;
        return g;
    }

    /// <summary>
    /// Barra de progresso. O ProgressBar do Windows ignora cor quando o tema visual está
    /// ligado e fica com trilho branco no meio da tela escura — dois Panels resolvem.
    /// </summary>
    public static Control BarraProgresso(double pct, Color cor) {
        double p = Math.Max(0, Math.Min(100, pct));
        var fora = new Panel { BackColor = LINE, Height = 8 };
        var dentro = new Panel { BackColor = cor, Dock = DockStyle.Left };
        fora.Controls.Add(dentro);
        fora.Resize += (o, e) => dentro.Width = (int)(fora.Width * p / 100);
        return fora;
    }

    /// <summary>Título de seção entre um bloco e outro.</summary>
    public static Control Secao(string s) {
        return new Label {
            Text = s, Height = 26, ForeColor = FG, Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Padding = new Padding(0, 6, 0, 0),
        };
    }

    /// <summary>Linha de botões, da esquerda pra direita.</summary>
    public static Control Barra(params Control[] cs) {
        var f = new FlowLayoutPanel { Height = 38, BackColor = Color.Transparent, WrapContents = false };
        foreach (var c in cs) { c.Margin = new Padding(0, 0, 8, 0); f.Controls.Add(c); }
        return f;
    }

    public static string Iniciais(string nome) {
        var ws = (nome ?? "").Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (ws.Length == 0) return "?";
        return (ws[0].Substring(0, 1) + (ws.Length > 1 ? ws[1].Substring(0, 1) : "")).ToUpper();
    }

    /// <summary>Status da parcela, do jeito que a etiqueta colorida mostrava.</summary>
    public static string Status(Parcela x, out Color cor) {
        if (x.pago) { cor = OK; return "paga"; }
        int d = Calc.DiasAte(x.venc);
        if (d < 0)  { cor = BAD;  return (-d) + "d atrasada"; }
        if (d == 0) { cor = WARN; return "vence hoje"; }
        if (d <= 5) { cor = WARN; return "em " + d + "d"; }
        cor = MUTED; return "em dia";
    }
}

}
