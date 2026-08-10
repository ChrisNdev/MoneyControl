// Bateria de verificação. Roda com: MoneyControl.exe --test
// Escreve o resultado em testes.txt e devolve código de saída 0 (ok) ou 1 (falhou).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace MoneyControl {

public static class Testes {
    static int total, falhas;
    static readonly StringBuilder log = new StringBuilder();

    static void Ok(bool cond, string oQueQuebrou) {
        total++;
        if (!cond) { falhas++; log.AppendLine("FALHOU: " + oQueQuebrou); }
    }

    public static int Rodar() {
        Datas();
        Centavos();
        ParcelasNormais();
        Assinaturas();
        TotaisPorPessoa();
        Serializacao();
        BackupComSenha();
        DpapiIdaEVolta();
        Telas();

        log.AppendLine(string.Format("{0} verificações, {1} falha(s)", total, falhas));
        try { File.WriteAllText(Path.Combine(Path.GetDirectoryName(Aplicacao.Exe), "testes.txt"), log.ToString()); }
        catch { }
        Console.Write(log.ToString());
        return falhas == 0 ? 0 : 1;
    }

    static void Datas() {
        Ok(Calc.AddMeses("2026-01-31", 1) == "2026-02-28", "mês curto: 31/01 + 1 deveria virar 28/02");
        Ok(Calc.AddMeses("2024-01-31", 1) == "2024-02-29", "ano bissexto: 31/01/2024 + 1 = 29/02");
        Ok(Calc.AddMeses("2026-12-05", 1) == "2027-01-05", "virada de ano");
        Ok(Calc.AddMeses("2026-08-10", 5) == "2027-01-10", "soma de 5 meses");
        Ok(Calc.AddMeses("2026-03-31", -1) == "2026-02-28", "subtrair mês");
        Ok(Calc.MesLabel("2026-08") == "ago/2026", "rótulo do mês");
        Ok(Calc.Fmt("2026-08-09") == "09/08/2026", "data formatada");
        Ok(Calc.Venc1("2026-08-20", 10) == "2026-09-10", "1º vencimento cai no dia da fatura");
        Ok(Calc.Venc1("2026-01-31", 31) == "2026-02-28", "1º vencimento não estoura mês curto");
    }

    static void Centavos() {
        var casos = new[] {
            new { t = 100.0, n = 3 }, new { t = 0.05, n = 3 },
            new { t = 1200.0, n = 12 }, new { t = 180.0, n = 2 }, new { t = 0.01, n = 1 },
        };
        foreach (var c in casos) {
            var p = Calc.Dividir(c.t, c.n);
            Ok(p.Length == c.n, "quantidade de parcelas errada");
            Ok(Math.Round(p.Sum() * 100) == Math.Round(c.t * 100),
               string.Format("soma das parcelas != total em {0}/{1}", c.t, c.n));
        }
        // comparar valor a valor, não o texto: "33.33" vira "33,33" numa máquina em pt-BR
        var sobra = Calc.Dividir(100, 3);
        Ok(sobra[0] == 33.33 && sobra[1] == 33.33 && sobra[2] == 33.34, "última parcela deve absorver a sobra");
        Ok(Calc.Dividir(300, 3).All(v => v == 100), "divisão exata");
    }

    static Lanc L(string id, string pid, double total, int n, string first) {
        return new Lanc { id = id, pid = pid, name = "X", total = total, n = n, first = first, date = first };
    }

    static void ParcelasNormais() {
        var pagos = new Dictionary<string, string> { { "p1:1", "2026-08-10" } };
        var ps = Calc.Parcelas(new List<Lanc> { L("p1", "joao", 300, 3, "2026-08-10") }, pagos);
        Ok(ps.Count == 3, "3 parcelas");
        Ok(ps[0].venc == "2026-08-10" && ps[1].venc == "2026-09-10" && ps[2].venc == "2026-10-10", "vencimentos mensais");
        Ok(ps[0].pago && !ps[1].pago, "status de pago veio da chave");
        Ok(ps[1].v == 100, "valor da parcela");
        Ok(ps[1].i == 2 && ps[1].n == 3, "numeração da parcela");

        // ordenação por vencimento entre lançamentos diferentes
        var mix = Calc.Parcelas(new List<Lanc> {
            L("b", "x", 100, 1, "2026-12-01"), L("a", "x", 100, 1, "2026-01-01"),
        }, new Dictionary<string, string>());
        Ok(mix[0].venc == "2026-01-01", "parcelas fora de ordem de vencimento");
    }

    static void Assinaturas() {
        string ini3 = Calc.AddMeses(Calc.Hoje(), -3), atual = Calc.MesDe(Calc.Hoje());
        var ass = new Lanc { id = "a", pid = "Netflix", name = "Netflix", total = 39.9, rec = true, first = ini3 };

        var r = Calc.Parcelas(new List<Lanc> { ass },
            new Dictionary<string, string> { { "a:" + Calc.MesDe(ini3), ini3 } });
        Ok(r.Count == 4, "assinatura deveria gerar 3 meses atrás + o atual");
        Ok(Calc.MesDe(r[r.Count - 1].venc) == atual, "assinatura passou do mês atual — vira lista infinita");
        Ok(r.All(x => x.v == 39.9 && x.rec), "assinatura: valor mensal fixo");
        Ok(r[0].pago && !r[1].pago, "assinatura: chave de pago é por mês");

        var cancelada = new Lanc {
            id = "a", pid = "Netflix", name = "N", total = 39.9, rec = true,
            first = ini3, ate = Calc.MesDe(Calc.AddMeses(Calc.Hoje(), -2)),
        };
        Ok(Calc.Parcelas(new List<Lanc> { cancelada }, new Dictionary<string, string>()).Count == 2,
           "assinatura cancelada ignorou o mês final");

        var futura = new Lanc { id = "a", pid = "X", name = "N", total = 10, rec = true, first = Calc.AddMeses(Calc.Hoje(), 2) };
        Ok(Calc.Parcelas(new List<Lanc> { futura }, new Dictionary<string, string>()).Count == 1,
           "assinatura que começa no futuro deveria mostrar a 1ª cobrança");

        // dia 31 numa assinatura não pode pular fevereiro
        var d31 = new Lanc { id = "a", pid = "X", name = "N", total = 10, rec = true, first = "2026-01-31", ate = "2026-03" };
        var p31 = Calc.Parcelas(new List<Lanc> { d31 }, new Dictionary<string, string>());
        Ok(p31.Count == 3 && p31[1].venc == "2026-02-28", "assinatura no dia 31 quebrou em fevereiro");
    }

    static void TotaisPorPessoa() {
        var s = new Estado();
        s.people.Add(new Pessoa { id = "joao", nome = "João" });
        s.people.Add(new Pessoa { id = "maria", nome = "Maria" });
        s.buys.Add(L("p1", "joao", 300, 3, "2026-08-10"));
        s.buys.Add(L("p2", "maria", 180, 2, "2026-08-10"));
        s.paid["p1:1"] = "2026-08-10";

        var ps = Calc.Parcelas(s.buys, s.paid);
        var t = Calc.Totais(ps, Calc.ColunasCartao(s));
        var joao = t.First(x => x.id == "joao");
        var maria = t.First(x => x.id == "maria");
        Ok(joao.gasto == 300 && joao.pago == 100 && joao.deve == 200, "totais do João");
        Ok(joao.abertas == 2 && joao.prox.i == 2, "próxima parcela em aberto do João");
        Ok(maria.gasto == 180 && maria.pago == 0 && maria.deve == 180, "totais da Maria");
        // João deve 200 e Maria 180: quem deve mais vem primeiro
        Ok(t[0].id == "joao", "ordenação deveria ser do que mais deve para o que menos deve");

        Ok(Calc.CorDoNome("Banco Y") == Calc.CorDoNome("Banco Y"), "cor do credor tem que ser estável");
        var sd = new Estado();
        sd.debts.Add(L("d1", "Banco Y", 100, 1, "2026-08-01"));
        sd.debts.Add(L("d2", "Banco Y", 100, 1, "2026-09-01"));
        Ok(Calc.ColunasDividas(sd).Count == 1, "credor repetido deveria virar uma coluna só");
    }

    static void Serializacao() {
        var s = new Estado();
        s.people.Add(new Pessoa { id = "p", nome = "João é ç", cor = "#10b981", fone = "11999998888" });
        s.buys.Add(new Lanc { id = "b", pid = "p", name = "Assinatura ão", total = 39.9, rec = true, first = "2026-01-31", ate = "2026-06" });
        s.paid["b:2026-01"] = "2026-01-31";
        s.cfg.due = 7; s.cfg.limit = 2500.50;

        var volta = Cofre.DeJson(Cofre.ParaJson(s));
        Ok(volta.people[0].nome == "João é ç", "acentos perdidos no JSON");
        Ok(volta.buys[0].rec && volta.buys[0].ate == "2026-06", "campos da assinatura perdidos");
        Ok(volta.buys[0].total == 39.9, "valor perdeu precisão");
        Ok(volta.paid.ContainsKey("b:2026-01"), "dicionário de pagos perdido");
        Ok(volta.cfg.due == 7 && volta.cfg.limit == 2500.50, "configuração perdida");

        // arquivo torto não pode derrubar o programa
        Ok(Cofre.DeJson("{}").people.Count == 0, "JSON vazio deveria virar estado vazio");
        Ok(Cofre.DeJson("{\"cfg\":{\"due\":99}}").cfg.due == 10, "dia de fatura fora da faixa não foi corrigido");
    }

    static void BackupComSenha() {
        var s = new Estado();
        s.people.Add(new Pessoa { id = "p", nome = "Segredo ção" });
        s.debts.Add(L("d", "Banco", 1200, 12, "2026-09-05"));

        var arq = Cofre.SelarComSenha(s, "senha-do-backup");
        Ok(!Encoding.UTF8.GetString(arq).Contains("Segredo"), "backup vazou texto puro!");

        var volta = Cofre.AbrirComSenha(arq, "senha-do-backup");
        Ok(volta.people[0].nome == "Segredo ção", "backup não reabriu igual");
        Ok(volta.debts.Count == 1, "backup perdeu as dívidas");

        bool recusou = false;
        try { Cofre.AbrirComSenha(arq, "chute"); } catch (CryptographicException) { recusou = true; }
        Ok(recusou, "backup abriu com senha errada!");

        // adulterar um byte do cifrado tem que ser detectado pelo HMAC
        var ruim = (byte[])arq.Clone();
        ruim[ruim.Length - 1] ^= 1;
        bool detectou = false;
        try { Cofre.AbrirComSenha(ruim, "senha-do-backup"); } catch (CryptographicException) { detectou = true; }
        Ok(detectou, "adulteração do backup passou sem detectar!");

        // adulterar o salt também
        var ruim2 = (byte[])arq.Clone();
        ruim2[5] ^= 1;
        bool detectou2 = false;
        try { Cofre.AbrirComSenha(ruim2, "senha-do-backup"); } catch (CryptographicException) { detectou2 = true; }
        Ok(detectou2, "salt adulterado passou sem detectar!");

        // dois backups do mesmo dado não podem sair iguais (salt e IV novos)
        var arq2 = Cofre.SelarComSenha(s, "senha-do-backup");
        Ok(!arq.SequenceEqual(arq2), "dois backups idênticos: salt/IV repetidos!");

        bool rejeitou = false;
        try { Cofre.AbrirComSenha(Encoding.ASCII.GetBytes("qualquer coisa"), "x"); }
        catch (InvalidDataException) { rejeitou = true; }
        Ok(rejeitou, "arquivo alheio deveria ser rejeitado com mensagem clara");
    }

    /// <summary>
    /// Monta cada tela com dados de verdade. Não olha pixel nenhum — só garante que nenhuma
    /// aba estoura, que é o defeito que aparece direto na cara do usuário.
    /// </summary>
    static void Telas() {
        var s = new Estado();
        s.people.Add(new Pessoa { id = "joao", nome = "João Silva", cor = "#7c7cf5", fone = "11999998888" });
        s.people.Add(new Pessoa { id = "maria", nome = "Maria", cor = "#10b981", fone = "" });
        s.buys.Add(L("p1", "joao", 300, 3, Calc.AddMeses(Calc.Hoje(), -1)));
        s.buys.Add(L("p2", "maria", 180, 2, Calc.Hoje()));
        s.buys.Add(new Lanc { id = "p3", pid = "joao", name = "Netflix", total = 39.9, rec = true,
                              first = Calc.AddMeses(Calc.Hoje(), -2), date = Calc.Hoje(), cat = "Assinaturas" });
        s.paid["p1:1"] = Calc.Hoje();
        s.debts.Add(L("d1", "Banco Y", 1200, 12, Calc.AddMeses(Calc.Hoje(), -3)));
        s.dpaid["d1:1"] = Calc.Hoje();
        s.cfg.limit = 2000;

        bool montou = true;
        string erro = "";
        try {
            using (var j = new Janela(s)) j.MontarTodasAsTelas();
        } catch (Exception e) { montou = false; erro = e.GetType().Name + ": " + e.Message; }
        Ok(montou, "uma das telas estourou ao ser montada — " + erro);

        // estado vazio também tem que desenhar: é a primeira coisa que o usuário novo vê
        bool vazio = true;
        try { using (var j = new Janela(new Estado())) j.MontarTodasAsTelas(); }
        catch (Exception e) { vazio = false; erro = e.Message; }
        Ok(vazio, "as telas estouraram com o programa vazio — " + erro);

        // as caixas de cadastro montam? nenhuma é aberta aqui, só construída
        bool caixas = true;
        try {
            using (var f = new FormLanc(s, true, null)) { }
            using (var f = new FormLanc(s, true, s.buys[0])) { }        // compra parcelada
            using (var f = new FormLanc(s, true, s.buys[2])) { }        // assinatura
            using (var f = new FormLanc(s, false, s.debts[0])) { }
            using (var f = new FormLanc(new Estado(), false, null)) { } // sem credor nenhum
            using (var f = new FormPessoa(s, null)) { }
            using (var f = new FormPessoa(s, s.people[0])) { }
        } catch (Exception e) { caixas = false; erro = e.GetType().Name + ": " + e.Message; }
        Ok(caixas, "uma caixa de cadastro estourou ao ser montada — " + erro);
    }

    static void DpapiIdaEVolta() {
        // grava e lê de verdade, num arquivo temporário, sem tocar nos dados do usuário
        string real = Cofre.Arquivo, tmp = real + ".teste";
        bool tinha = File.Exists(real);
        string guardado = tinha ? real + ".guardado" : null;
        try {
            if (tinha) File.Copy(real, guardado, true);

            var s = new Estado();
            s.people.Add(new Pessoa { id = "p", nome = "Confidencial" });
            Cofre.Salvar(s);

            var bruto = File.ReadAllBytes(Cofre.Arquivo);
            Ok(!Encoding.UTF8.GetString(bruto).Contains("Confidencial"), "arquivo em disco vazou texto puro!");
            Ok(Cofre.Carregar().people[0].nome == "Confidencial", "não releu o que gravou");

            // gravar de novo por cima não pode perder nada (caminho do File.Replace)
            s.people.Add(new Pessoa { id = "q", nome = "Outro" });
            Cofre.Salvar(s);
            Ok(Cofre.Carregar().people.Count == 2, "regravação por cima perdeu dados");
            Ok(!File.Exists(Cofre.Arquivo + ".tmp"), "sobrou arquivo temporário depois de gravar");
        } finally {
            try {
                if (tinha) { File.Copy(guardado, real, true); File.Delete(guardado); }
                else if (File.Exists(real)) File.Delete(real);
                if (File.Exists(tmp)) File.Delete(tmp);
            } catch { }
        }
    }
}

}
