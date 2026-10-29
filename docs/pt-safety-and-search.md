# PT: conferência, ciência, comandos e pesquisa

Implementação vinculada aos cards [requisitos](https://trello.com/c/Qfr34Qle), [ciência](https://trello.com/c/xK5dhxAa), [liberação](https://trello.com/c/n3fXiUnV), [acompanhamento](https://trello.com/c/O0jn3LWy) e [busca](https://trello.com/c/w9vpbSC7) do quadro SESMT — Gestão Documental.

## Comandos com versão explícita

Os comandos abaixo conservam as rotas `/work-permits/{id}/...` e possuem aliases `/pts/{id}/...`. A identidade do ator vem do JWT; escopo, papel e designação são lidos no servidor. Comandos não aceitam liberação implícita por administrador.

`POST release` exige:

```json
{
  "expectedVersion": 7,
  "conditions": "Condições demonstrativas apresentadas pelo responsável",
  "authorizedFrom": "2030-01-10T13:00:00Z",
  "authorizedUntil": "2030-01-10T17:00:00Z",
  "confirmation": true
}
```

A janela precisa estar contida no agendamento confirmado e terminar no futuro. `POST start` e `POST resume` recebem `{ "expectedVersion": 8 }` e exigem que o instante atual pertença à janela liberada. Suspender/cancelar recebem `{ "expectedVersion": 9, "reason": "Motivo" }`. Versão divergente produz conflito; repetir um comando antigo não cria outra decisão.

Leitura das condições, autorização, avaliação e gravação dos comandos acontecem em transação serializável. Conflitos de serialização exigem recarregar e confirmar novamente; não há repetição automática de uma decisão humana.

## Elegibilidade administrativa

`GET /pts/{id}/eligibility` retorna `{ "ready": false, "issues": [...] }` com motivos administrativos. O mesmo serviço é executado novamente ao liberar, iniciar e retomar.

- Cada serviço selecionado precisa possuir matriz ativa; a evidência precisa atender à união dos tipos exigidos.
- Cada participante precisa ter usuário ativo no escopo da empresa, ciência aplicável e suas próprias evidências. O titular é obtido por `Worker.UserId` e vínculo `TRABALHADOR`; `Document.OwnerId` identifica o administrador documental, não o titular.
- O documento precisa estar vinculado à PT e ao trabalhador correspondente. A versão mais recente precisa estar vigente e conferida, com registro de arquivo ativo e hash correspondente. Upload, versão em análise/reprovada e uma versão anterior aceita não aprovam a versão nova.
- A validade é conferida para o período agendado, com o início ajustado para o dia atual quando o serviço já começou. Datas sem vencimento continuam sem prazo artificial.
- Serviços e participantes configurados no agendamento precisam corresponder ao contexto da PT. Um agendamento legado ainda solicitado não libera execução.

Os motivos distinguem ausência, espera de conferência, reprovação, arquivo indisponível e validade incompatível. Essa avaliação não determina aptidão clínica e não devolve arquivos médicos ou seu conteúdo.

## Ciência individual e identificação assistida

`GET /pts/{id}/declarations` informa `expectedVersion`, `contentVersion`, declarações dos templates fixados e o contexto do agendamento. `POST /pts/{id}/acknowledgements` recebe:

```json
{
  "expectedVersion": 5,
  "contentVersion": 3,
  "participantUserId": "00000000-0000-4000-8000-000000000001",
  "confirmedName": "Participante fictício",
  "identificationMethod": "Identificação assistida fictícia pelo técnico",
  "confirmation": true
}
```

O participante autenticado registra a própria ciência. A coleta para outra pessoa exige operador responsável pelo atendimento e designação vigente. São conservados participante, coletor, nome apresentado, método de identificação, conteúdo, versão e horário. A representação nominal é apenas registro administrativo; desenho e assinatura certificada não foram implementados.

Alterar participantes, checklist ou vínculo documental muda a versão de conteúdo. Alterar o agendamento muda o contexto apresentado: o servidor compara o snapshot atual ao texto registrado e exige nova ciência mesmo que `contentVersion` não tenha mudado. Evidências anteriores permanecem no histórico.

## Observação e conclusão

`POST /pts/{id}/observations` recebe `{ "expectedVersion": 10, "observation": "Registro do andamento" }`. É permitido ao operador responsável/designado durante execução ou suspensão; registros são acrescentados, sem substituir os anteriores.

Templates podem configurar `closingQuestions`, usando o mesmo formato de perguntas tipadas da preparação. Novas versões não podem remover nem enfraquecer verificações de encerramento obrigatórias/impeditivas. A conclusão exige configurações de encerramento em todos os templates fixados.

`POST /pts/{id}/complete` recebe:

```json
{
  "expectedVersion": 11,
  "summary": "Resumo do que foi efetivamente realizado",
  "actualEnd": "2030-01-10T16:30:00Z",
  "closingAnswers": [
    { "perguntaId": "00000000-0000-4000-8000-000000000002", "valor": "true" }
  ]
}
```

Sem início válido, resumo, horário coerente ou verificações atendidas, o comando é recusado sem conclusão parcial. A conclusão conserva o realizado, marca o estado operacional `CONCLUIDA` e a revisão `AGUARDANDO_REVISAO`. Ela não assina, aprova nem reautoriza execução. A revisão final, representação do engenheiro e PDF imutável continuam dependências específicas do card de revisão/exportação.

## Busca paginada sem ampliar o escopo

`GET /work-permits` continua retornando o array legado. `GET /work-permits/search` e `GET /pts` retornam `{ "items": [], "total": 0, "page": 1, "pageSize": 20 }`.

Filtros combináveis: `companyId`, `search` (ou `q`), `serviceCode`, `participantUserId`, `locationId`, `from`, `to`, `status`, `page` e `pageSize`. O período usa sobreposição com o agendamento. O texto tem até 200 caracteres; páginas são positivas e `pageSize` fica entre 1 e 100. Situação inválida ou intervalo invertido retorna 400. Uma empresa informada fora do escopo retorna 404.

O SQL aplica escopo antes da contagem e paginação, com ordenação estável. `GET /pts/{id}/timeline` é um alias do histórico já protegido.

## Limites e validação

Matriz por função e seu versionamento completo, desenho protegido, revisão/representação final e PDF aprovado não estão concluídos por este pacote. Templates permanecem ilustrativos até validação formal. Configurações de envio de e-mails não foram alteradas.

Os testes cobrem união de serviços e participantes, pendências, validade, preservação de ciências, mudança de programação, janela autorizada, versão concorrente, designação, conclusão sem aprovação e isolamento de contagem. Os testes PostgreSQL/API e sua execução são documentados separadamente em `postgresql-integration-tests.md`.
