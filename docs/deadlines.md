# Painel de vencimentos

`GET /api/v1/deadlines` reúne, para todas as unidades que o usuário pode consultar, os prazos administrativos que vencem ou já venceram:

| `kind` | Origem | Data usada | Permissão exigida |
| --- | --- | --- | --- |
| `TRAINING` | Registro de treinamento | `renewalOn` | `treinamento:ler` |
| `ASO` | Registro de ASO | `nextEvaluationOn` | `aso:acompanhar` |
| `EPI` | Entrega de EPI | `replacementOn` | `epi:ler` |

Documentos com vigência já têm filtro próprio (`validUntilBefore` na busca documental), que aplica a política de sigilo dos documentos. Por isso não entram nesta consulta.

## Parâmetros

- `days`: janela futura, de 1 a 365 dias. O padrão é 30. Itens já vencidos sempre aparecem.
- `unitId`, `workerId`: filtros opcionais. Uma unidade fora do escopo devolve 403.
- `kind`: `TRAINING`, `ASO` ou `EPI`. Pedir uma origem sem a permissão correspondente devolve 403.

## Resposta

```json
{
  "referenceDate": "2026-11-14",
  "days": 30,
  "kinds": ["TRAINING", "EPI"],
  "summary": { "overdue": 3, "dueSoon": 5 },
  "total": 8,
  "truncated": false,
  "items": [
    { "kind": "TRAINING", "id": "…", "companyId": "…", "unitId": "…", "unitName": "Unidade Centro",
      "workerId": "…", "workerName": "…", "label": "NR-35", "dueOn": "2026-11-01", "status": "OVERDUE" }
  ]
}
```

`kinds` lista as origens que o usuário pode ler. Uma origem ausente significa falta de permissão, não ausência de vencimentos. Os itens vêm ordenados pela data. A lista para em 200 itens (`truncated: true`), mas `summary` e `total` contam todos os itens.

## Regras

- A data de referência é o dia atual em `America/Sao_Paulo`. `OVERDUE` significa data anterior a hoje; `DUE_SOON`, entre hoje e hoje + `days`.
- Cada origem aplica a mesma regra de leitura do seu módulo: empresa com permissão, unidade inteira ou setor do escopo e, para contas restritas ao próprio registro, somente o próprio trabalhador.
- Só o registro atual de cada assunto é considerado:
  - treinamento: o mais recente do trabalhador com o mesmo título (sem diferenciar maiúsculas de minúsculas);
  - ASO: o mais recente do trabalhador;
  - EPI: a entrega mais recente do mesmo equipamento ao trabalhador.

  Registros revogados e EPIs devolvidos são ignorados. Um item já renovado não gera alerta.
- Ficam de fora trabalhadores inativos e os que têm desligamento até hoje.
- Do ASO, o painel informa apenas a data da próxima avaliação. Tipo de exame, aptidão e dados clínicos ou do emitente não são expostos. O perfil ADMIN não recebe `aso:acompanhar` por padrão e, por isso, não vê os itens de ASO.
- O painel não conclui aptidão, qualificação ou liberação para o trabalho. Ele só mostra datas que já foram registradas.
