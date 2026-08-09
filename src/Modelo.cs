// Modelo de dados e regras de cálculo. Sem UI aqui — é o que os testes cobrem.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CardControl {

public class Pessoa {
    public string id = "", nome = "", cor = "#7c7cf5", fone = "";
}

/// <summary>
/// Uma compra do cartão ou uma dívida pessoal — a forma é a mesma.
/// No cartão, pid é o id da pessoa; nas dívidas, o nome do credor.
/// </summary>
public class Lanc {
    public string id = "", pid = "", name = "", date = "", first = "", cat = "Outros";
    public double total;
    public int n = 1;
    public bool rec;        // assinatura: repete todo mês
    public string ate = ""; // YYYY-MM, opcional: mês em que a assinatura acaba
}

public class Cfg {
    public int due = 10;      // dia do vencimento da fatura
    public double limit = 0;  // limite do cartão (0 = ocultar)
}

public class Estado {
    public List<Pessoa> people = new List<Pessoa>();
    public List<Lanc> buys = new List<Lanc>();
    public Dictionary<string, string> paid = new Dictionary<string, string>();
    public List<Lanc> debts = new List<Lanc>();
    public Dictionary<string, string> dpaid = new Dictionary<string, string>();
    public Cfg cfg = new Cfg();
}

/// <summary>Uma parcela derivada de um lançamento. Nada disso é gravado: sempre recalculado.</summary>
public class Parcela {
    public string chave, lancId, pid, nome;
    public int i, n;
    public double v;
    public string venc;   // YYYY-MM-DD
    public bool pago, rec;
}

public class TotalPor {
    public string id, nome, cor;
    public double gasto, pago, deve;
    public int abertas;
    public Parcela prox;
}

public static class Calc {
    public static readonly string[] CATS = {
        "Alimentação","Mercado","Eletrônicos","Roupas","Transporte","Assinaturas","Lazer","Outros"
    };
    public static readonly string[] CORES = {
        "#7c7cf5","#10b981","#f59e0b","#ef4444","#06b6d4","#ec4899","#8b5cf6","#84cc16"
    };

    public static string Hoje() { return DateTime.Now.ToString("yyyy-MM-dd"); }
    public static string MesDe(string ymd) { return ymd.Substring(0, 7); }
    public static int DiasNoMes(int y, int m) { return DateTime.DaysInMonth(y, m); }

    /// <summary>Soma k meses sem estourar mês curto: 31/01 + 1 = 28/02.</summary>
    public static string AddMeses(string ymd, int k) {
        int y = int.Parse(ymd.Substring(0, 4)), m = int.Parse(ymd.Substring(5, 2)), d = int.Parse(ymd.Substring(8, 2));
        int t = y * 12 + m - 1 + k;
        int ny = t / 12, nm = t % 12 + 1;
        int nd = Math.Min(d, DiasNoMes(ny, nm));
        return string.Format("{0:D4}-{1:D2}-{2:D2}", ny, nm, nd);
    }

    public static int DiasAte(string ymd) {
        return (int)(Data(ymd) - Data(Hoje())).TotalDays;
    }

    public static DateTime Data(string ymd) {
        return DateTime.ParseExact(ymd, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    static readonly string[] MESES = { "jan","fev","mar","abr","mai","jun","jul","ago","set","out","nov","dez" };
    public static string MesLabel(string ym) {
        return MESES[int.Parse(ym.Substring(5, 2)) - 1] + "/" + ym.Substring(0, 4);
    }

    public static string Fmt(string ymd) {
        if (string.IsNullOrEmpty(ymd)) return "—";
        return ymd.Substring(8, 2) + "/" + ymd.Substring(5, 2) + "/" + ymd.Substring(0, 4);
    }

    public static string Brl(double v) {
        return v.ToString("C2", CultureInfo.GetCultureInfo("pt-BR"));
    }

    /// <summary>Divide sem perder centavo: a última parcela absorve a sobra.</summary>
    public static double[] Dividir(double total, int n) {
        long c = (long)Math.Round(total * 100.0, MidpointRounding.AwayFromZero);
        long baseC = c / n;
        var outp = new double[n];
        for (int i = 0; i < n; i++) outp[i] = baseC / 100.0;
        outp[n - 1] = (c - baseC * (n - 1)) / 100.0;
        return outp;
    }

    /// <summary>
    /// Parcelas de uma lista de lançamentos, ordenadas por vencimento.
    ///
    /// Assinatura não tem fim, então seria infinita: geramos só até o mês atual — o que
    /// já foi cobrado de verdade. `ate` encerra antes disso quando foi cancelada.
    /// Para planejar meses à frente, é aqui que se soma N ao limite.
    /// </summary>
    public static List<Parcela> Parcelas(List<Lanc> lista, Dictionary<string, string> pagos) {
        var outp = new List<Parcela>();
        string atual = MesDe(Hoje());
        foreach (var b in lista) {
            if (b.rec) {
                string fim = string.Compare(atual, MesDe(b.first), StringComparison.Ordinal) < 0 ? MesDe(b.first) : atual;
                if (!string.IsNullOrEmpty(b.ate) && string.Compare(b.ate, fim, StringComparison.Ordinal) < 0) fim = b.ate;
                string venc = b.first;
                int i = 1;
                while (string.Compare(MesDe(venc), fim, StringComparison.Ordinal) <= 0) {
                    // chave por mês: não quebra se a data de início mudar depois
                    string k = b.id + ":" + MesDe(venc);
                    outp.Add(new Parcela {
                        chave = k, lancId = b.id, pid = b.pid, nome = b.name, i = i, n = 0,
                        v = b.total, venc = venc, pago = pagos.ContainsKey(k), rec = true,
                    });
                    venc = AddMeses(venc, 1);
                    i++;
                }
                continue;
            }
            int qtd = Math.Max(1, b.n);
            var vals = Dividir(b.total, qtd);
            for (int i = 1; i <= qtd; i++) {
                string k = b.id + ":" + i;
                outp.Add(new Parcela {
                    chave = k, lancId = b.id, pid = b.pid, nome = b.name, i = i, n = qtd,
                    v = vals[i - 1], venc = AddMeses(b.first, i - 1), pago = pagos.ContainsKey(k), rec = false,
                });
            }
        }
        return outp.OrderBy(p => p.venc, StringComparer.Ordinal).ToList();
    }

    /// <summary>Totais por pessoa (cartão) ou por credor (dívidas), do que mais deve para o que menos deve.</summary>
    public static List<TotalPor> Totais(List<Parcela> ps, List<TotalPor> colunas) {
        string agora = Hoje();
        foreach (var c in colunas) {
            var minhas = ps.Where(p => p.pid == c.id).ToList();
            var abertas = minhas.Where(p => !p.pago).ToList();
            c.gasto = minhas.Sum(p => p.v);
            c.pago = minhas.Where(p => p.pago).Sum(p => p.v);
            c.deve = abertas.Sum(p => p.v);
            c.abertas = abertas.Count;
            c.prox = abertas.FirstOrDefault(p => string.Compare(p.venc, agora, StringComparison.Ordinal) >= 0)
                     ?? abertas.FirstOrDefault();
        }
        return colunas.OrderByDescending(c => c.deve).ToList();
    }

    /// <summary>Colunas do cartão: as pessoas cadastradas.</summary>
    public static List<TotalPor> ColunasCartao(Estado s) {
        return s.people.Select(p => new TotalPor { id = p.id, nome = p.nome, cor = p.cor }).ToList();
    }

    /// <summary>Colunas das dívidas: os credores, que são só nomes — cor derivada do próprio nome.</summary>
    public static List<TotalPor> ColunasDividas(Estado s) {
        return s.debts.Select(d => d.pid).Distinct().OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Select(c => new TotalPor { id = c, nome = c, cor = CorDoNome(c) }).ToList();
    }

    public static string CorDoNome(string s) {
        int soma = 0;
        foreach (char ch in s ?? "") soma += ch;
        return CORES[Math.Abs(soma) % CORES.Length];
    }

    public static string NovoId() {
        return Guid.NewGuid().ToString("N").Substring(0, 8);
    }

    /// <summary>Vencimento sugerido da 1ª parcela do cartão: mês seguinte, no dia da fatura.</summary>
    public static string Venc1(string dataCompra, int diaFatura) {
        string prox = AddMeses(dataCompra, 1);
        int y = int.Parse(prox.Substring(0, 4)), m = int.Parse(prox.Substring(5, 2));
        return string.Format("{0:D4}-{1:D2}-{2:D2}", y, m, Math.Min(diaFatura, DiasNoMes(y, m)));
    }
}

}
