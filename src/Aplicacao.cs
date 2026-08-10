// Ponto de entrada, avisos e backup. A parte visual mora em Ui.cs.
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace MoneyControl {

public static class Aplicacao {
    public const string VERSAO = "5.0";

    public static string Exe { get { return Assembly.GetExecutingAssembly().Location; } }

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
        MessageBox.Show(msg, "MoneyControl", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    public static void Aviso(string msg) {
        MessageBox.Show(msg, "MoneyControl", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public static bool Confirma(string msg) {
        return MessageBox.Show(msg, "MoneyControl", MessageBoxButtons.YesNo, MessageBoxIcon.Question)
               == DialogResult.Yes;
    }

    /// <summary>Caixa de texto de uma linha. Devolve null se o usuário desistir.</summary>
    public static string PedirTexto(string titulo, string texto, bool senha) {
        using (var f = new Form {
            Text = titulo, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false,
            MinimizeBox = false, StartPosition = FormStartPosition.CenterParent, ShowInTaskbar = false,
            ClientSize = new Size(430, 168), BackColor = Ui.BG, ForeColor = Ui.FG, Font = Ui.F(14),
        }) {
            var lbl = new Label {
                Text = texto, Location = new Point(20, 18), Size = new Size(390, 48), ForeColor = Ui.FG2,
                Font = Ui.F(13),
            };
            var txt = new TextBox {
                UseSystemPasswordChar = senha, Location = new Point(22, 76), Size = new Size(386, 26),
                BackColor = Ui.FIELD, ForeColor = Ui.FG, BorderStyle = BorderStyle.FixedSingle, Font = Ui.F(14),
            };
            var caixa = new Card {
                Location = new Point(20, 70), Size = new Size(390, 40), Raio = 14,
                Fundo = Ui.FIELD, Borda = Ui.LINE, BackColor = Ui.BG,
            };
            var ok = new Botao("Confirmar", null, true) { BackColor = Ui.BG };
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(410 - ok.Width, 118);
            var cancel = new Botao("Cancelar") { BackColor = Ui.BG };
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(410 - ok.Width - cancel.Width - 10, 118);

            f.Controls.AddRange(new Control[] { lbl, caixa, txt, ok, cancel });
            txt.BringToFront();
            f.AcceptButton = ok; f.CancelButton = cancel;
            return f.ShowDialog() == DialogResult.OK ? txt.Text : null;
        }
    }

    public static string PedirSenha(string titulo, string texto) { return PedirTexto(titulo, texto, true); }

    /* ---------------------------- backup ---------------------------- */

    // A data do último backup não cabe no modelo de dados (que não muda aqui) nem no
    // formato do arquivo: fica num bilhete ao lado, que só a interface lê.
    static string Bilhete { get { return Path.Combine(Cofre.Pasta, "ultimo-backup.txt"); } }

    public static string UltimoBackup {
        get { try { return File.ReadAllText(Bilhete).Trim(); } catch { return ""; } }
        set {
            try {
                Directory.CreateDirectory(Cofre.Pasta);
                File.WriteAllText(Bilhete, value);
            } catch { }
        }
    }

    /// <summary>
    /// Abre um backup do disco: `.mcb` protegido por senha ou `.json` em texto puro.
    /// Devolve null se o usuário desistir ou se o arquivo não servir.
    /// </summary>
    public static Estado RestaurarDeArquivo() {
        using (var dlg = new OpenFileDialog {
            Title = "Restaurar backup",
            Filter = "Backup do MoneyControl (*.mcb;*.json)|*.mcb;*.json|Todos os arquivos|*.*",
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

    /// <summary>Grava um `.mcb` com senha escolhida na hora — é o backup que abre em qualquer máquina.</summary>
    public static void ExportarBackup(Estado s) {
        string senha = PedirSenha("Backup criptografado",
            "Crie uma senha para este backup.\nÉ ela que vai abrir o arquivo em outro computador:");
        if (senha == null) return;
        if (senha.Length < 8) { Erro("Use pelo menos 8 caracteres."); return; }
        if (senha != PedirSenha("Backup criptografado", "Repita a senha do backup:")) {
            Erro("As senhas não bateram."); return;
        }
        using (var dlg = new SaveFileDialog {
            Title = "Salvar backup", Filter = "Backup do MoneyControl (*.mcb)|*.mcb",
            FileName = "moneycontrol-" + Calc.Hoje() + ".mcb",
        }) {
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try {
                File.WriteAllBytes(dlg.FileName, Cofre.SelarComSenha(s, senha));
                UltimoBackup = Calc.Hoje();
                Aviso("Backup salvo.\n\nGuarde a senha: sem ela o arquivo não abre, e não há recuperação.");
            } catch (Exception e) { Erro("Não deu pra exportar: " + e.Message); }
        }
    }

    public static void ExportarPlano(Estado s) {
        if (!Confirma("Este arquivo sai SEM criptografia — qualquer um que abrir vê tudo.\n\nContinuar?")) return;
        using (var dlg = new SaveFileDialog {
            Title = "Exportar sem criptografia", Filter = "JSON (*.json)|*.json",
            FileName = "moneycontrol-" + Calc.Hoje() + "-SEM-SENHA.json",
        }) {
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try { File.WriteAllText(dlg.FileName, Cofre.ParaJson(s), Encoding.UTF8); }
            catch (Exception e) { Erro("Não deu pra exportar: " + e.Message); }
        }
    }
}

}
