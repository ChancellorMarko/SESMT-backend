# Evolução mensal de atividades do dashboard

`GET /dashboard/activity-evolution` usa eventos imutáveis de transição de Permissões de Trabalho, que são o módulo com histórico de estados e data de ocorrência persistidos. Cada evento é contado uma vez no mês de `OccurredAt` (UTC); uma PT pode aparecer em meses diferentes quando tem transições distintas.

Mapeamento das transições:

| Estado de destino | Grupo |
| --- | --- |
| `EM_VERIFICACAO`, `LIBERADA`, `DEVOLVIDA` | `pendentes` |
| `EM_EXECUCAO`, `SUSPENSA` | `emAndamento` |
| `CONCLUIDA` | `concluidas` |
| `CANCELADA` | Fora dos três grupos |

O endpoint não trata versões documentais, cadastros de pessoas ou parâmetros administrativos como atividades: esses módulos ainda não possuem uma linha do tempo de estados com data de ocorrência adequada para uma série histórica sem inferir ou fabricar eventos. Quando esses módulos ganharem eventos explícitos, eles podem ser incluídos com seu próprio mapeamento documentado.

`from` e `to` são instantes UTC e limitam eventos dentro dos meses indicados. Sem período explícito, retorna os últimos 12 meses incluindo o mês atual. O intervalo máximo é de 36 meses. `localId`, `equipeId` e `servicoId` são revalidados com o mesmo escopo calculado para o dashboard; filtro fora do escopo resulta em 403. A resposta sempre inclui cada mês em ordem cronológica, mesmo sem eventos.

