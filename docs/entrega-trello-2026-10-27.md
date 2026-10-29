# Entrega do backlog — 27/10/2026

## Origem e escopo

Quadro consultado: [SESMT — Gestão Documental](https://trello.com/b/1tOlWQwr/sesmt-gest%C3%A3o-documental). Foram revisadas as listas **A fazer** e **Backlog**. O trabalho está restrito ao repositório [SESMT-backend](https://github.com/ChancellorMarko/SESMT-backend).

O checkout estava limpo em `main`. Foi executado `git pull --ff-only origin main`, de `5b42993` para `b868f6d01d436d1b46bd9f6964413de5f546079d`. As alterações desta entrega partem desse último commit e estão na branch local `codex/trello-backend-mvp`. Não foi realizado push destas alterações.

Configuração SMTP e serviços de envio de e-mails ficaram fora desta implementação. Os testes substituem o remetente por uma implementação que impede envios externos.

## Cards de backend e resultado

| Card do Backlog | Resultado desta entrega |
| --- | --- |
| [Documentos, Certificados e Arquivos Protegidos](https://trello.com/c/uCjvOHvX) | Implementação para usuários autenticados: autorização por ator/escopo, vínculo ao titular, upload PDF/PNG/JPEG com tipo e limite reais, armazenamento privado, hash e downloads autorizados. Upload continua pendente de conferência. Não há upload anônimo pelo convite. |
| [Conferência e Versões de Evidências](https://trello.com/c/2Z4HQWaL) | Decisão explícita na versão atual, motivo na reprovação, conflito de versão e preservação da anterior. Exige designação `conferir`; documentos restritos/médicos exigem concessão específica. Não existe bypass genérico de administrador. |
| [Busca e Consulta de Documentos e PTs](https://trello.com/c/w9vpbSC7) | Pesquisa paginada, critérios combinados e ordenação estável. Escopo aplicado antes da contagem. Busca geral exclui documentos restritos/médicos. Contratos legados de listagem foram preservados e há aliases `/pts`. |
| [Requisitos, Validade e Bloqueios da PT](https://trello.com/c/Qfr34Qle) | Avaliação fortalecida: união dos serviços, evidências próprias de cada participante, conferência da versão vigente, arquivo e validade do período. **Parcial:** a configuração existente por empresa/serviço ainda não constitui o cadastro completo de matrizes versionadas por função previsto no card. |
| [Ciência dos Participantes e Identificação](https://trello.com/c/xK5dhxAa) | Ciência nominal explícita, com participante, coletor, declaração/contexto, versão e horário. Mudança relevante invalida aplicabilidade, conservando histórico. **Parcial:** desenho e identificação forte para uso real não foram implementados. |
| [Agenda, Solicitação e Confirmação](https://trello.com/c/OLyuMNKk) | Solicitação com múltiplos serviços, equipe e participantes; confirmação separada; capacidade por local/responsável; conflitos concorrentes; reagendamento/cancelamento com motivo e histórico. A agenda não modifica uma PT já vinculada nem autoriza execução. |
| [Convite por Link com Acesso Limitado](https://trello.com/c/bk9Yff7C) | Link manual opaco, hash persistido, expiração/revogação e escopo fixo da equipe. Pode solicitar e consultar somente seus pedidos; não autentica a identidade individual, não abre o dashboard e não registra ciência/liberação. |
| [Liberação Inicial e Início da Execução](https://trello.com/c/n3fXiUnV) | Versão e confirmação explícitas, condições e janela autorizada; papel/designação e requisitos revalidados na transação de liberação/início/retomada. Pendência ou conflito conserva o estado anterior. |
| [Acompanhamento e Conclusão Operacional](https://trello.com/c/O0jn3LWy) | Observações versionadas e conclusão com resumo, horário coerente e perguntas de encerramento dos templates fixados. Sem encerramento configurado, o comando é bloqueado. Concluir envia para `AGUARDANDO_REVISAO`, sem aprovação automática. |
| [Testes do MVP de PT Digital](https://trello.com/c/yvOS7THa) | Testes unitários e API/PostgreSQL isolado para migrações, autorização, documentos, concessões, versões, reservas concorrentes, convites, busca e histórico. **Parcial:** o ciclo completo até revisão final/PDF ainda não existe e não foi declarado validado. |
| [Revisão Final, Correções e Representação do Engenheiro](https://trello.com/c/O4CgZe8n) | **Pendente:** fila, devolução/correção/reapresentação, representação própria versionada e aprovação final. Não foram publicados endpoints ou aprovações simulando essa implementação. |
| [Documento Final e Exportação da PT](https://trello.com/c/Sb4gBCGD) | **Pendente:** snapshot aprovado imutável e PDF controlado, dependentes do fluxo de revisão e representação. |

Na lista **A fazer**, os itens funcionais encontrados eram `UX ➔ Jornada, Wireframes e Acessibilidade do MVP` e `🔹Front-end ➔ Gráfico de Evolução Mensal das Atividades`. Não foram alterados neste checkout de backend. Divisores de listas não foram tratados como funcionalidades. O card de validação de regras/templates permanece uma atividade de produto/SESMT.

Os cards não foram movidos, comentados nem marcados como concluídos no Trello. O quadro não é usado como evidência de homologação.

## Contratos e configuração

- [Documentos e conferência](protected-document-evidence.md): rotas, concessões, limites, armazenamento e versões.
- [PT, ciência, comandos e pesquisa](pt-safety-and-search.md): payloads, conflitos e bloqueios.
- [Testes PostgreSQL](postgresql-integration-tests.md): execução com banco isolado e configuração de CI.
- `POST /appointments`, `GET /appointments`, `GET /appointments/{id}` e comandos `confirm`, `reschedule`, `cancel`; `PUT /appointments/{id}/context` permite completar uma solicitação legada antes da confirmação, quando ainda não possui PT.
- `POST /invites`, `POST /invites/{id}/revoke`, `GET /invites/context`, `POST /invites/appointments` e `GET /invites/appointments/{id}`. O token público usa o header `X-Invite-Token`; a criação retorna o link relativo `/convite/{token}` uma única vez.
- Identificadores de participantes da agenda são `Worker.Id`. Participantes/ciências de PT usam o usuário vinculado (`Worker.UserId`); o servidor verifica a correspondência.
- `Appointments:LocationCapacity` e `ResponsibleCapacity` começam em 1; `MaximumInviteHours` começa em 168; `MaximumPendingRequestsPerInvite` começa em 20. São limites iniciais configuráveis, não uma política operacional homologada. A disponibilidade usa o pico simultâneo de ocupação e intervalos de término exclusivos.
- Papéis `SESMT_PT_SOLICITANTE`, `SESMT_PT_OPERADOR`, `SESMT_PT_LIBERADOR` e `SESMT_PT_CONFIG` são provisionados pelo seed, sem atribuição automática aos usuários. Escopos e designações continuam necessários.

## Migrações e preservação de dados

1. `20261027034549_CompleteMvpEvidenceAndSchedulingFoundations`: concessões documentais, controle de versão, dados de ciência/liberação/encerramento e agenda/convites.
2. `20261027035114_RepairHistoricalWorkerDepartment`: corrige uma divergência anterior na cadeia de migrações. O snapshot já esperava `core.trabalhador.setor_id`, mas um banco criado do zero não recebia a coluna. Usa `ADD COLUMN IF NOT EXISTS`; o rollback preserva a coluna para não remover atribuições mantidas anteriormente.

Documentos legados não são automaticamente considerados conferidos. Agendamentos legados começam como solicitados; os que não têm contexto válido permanecem bloqueados até regularização explícita. A entrega não executou migrations no banco configurado do usuário.

## Validação e limites

A suíte foi executada contra PostgreSQL 18.6 real em cluster local descartável, com banco aleatório por execução, dados fictícios e remoção do banco alocado ao terminar. O pacote inclui `VALIDACAO.txt` com o resultado final. O CI passa a oferecer PostgreSQL 18 para executar esses testes.

O teste inicial da `main` tinha uma falha: uma fixture de liberação não fornecia a designação que o código atual já exigia. A fixture agora estabelece a autorização necessária antes de testar a indisponibilidade da elegibilidade, e mantém testes de recusa sem designação.

A auditoria das tabelas/colunas é feita antes de popular os dados de teste; divergências não são suprimidas. Também há testes de isolamento por empresa, integridade do arquivo, revogação de concessão, convite expirado/revogado, versão concorrente e unicidade de PT por agendamento.

Esta entrega não homologa procedimentos de segurança, validade normativa, aptidão clínica, identidade forte ou assinatura certificada. Não configura antivírus, backup/restauração ou retenção do ambiente de produção. A conexão ponta a ponta com o frontend e a revisão/PDF permanecem etapas específicas, não resultados dos testes de API deste pacote.

## Aplicação do ZIP

O ZIP contém somente as alterações posteriores à base `b868f6d`, não um clone completo. `arquivos/` conserva os caminhos relativos; `alteracoes.patch` inclui arquivos adicionados, alterados e eventuais exclusões. `manifesto.json` informa status e hashes; `excluidos.txt` registra exclusões.

Em um checkout compatível, primeiro execute `git apply --check alteracoes.patch` e revise o manifesto; depois aplique o patch. Alternativamente, copie os arquivos para os caminhos indicados e trate eventuais exclusões explicitamente. As dependências são restauradas por `dotnet restore`. Configure o banco desejado e aplique migrations pelo procedimento normal do projeto, após revisar e preservar seus dados.

Runtime PostgreSQL descartável, credenciais locais, `.git`, `bin`, `obj` e resultados brutos dos testes não fazem parte do ZIP.
