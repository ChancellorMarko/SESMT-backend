# Entrega 2 — Projeto técnico do SESMT

## Escopo e situação

O SESMT usa .NET 10, Clean Architecture, PostgreSQL e EF Core 10. Esta especificação descreve a solução presente no repositório e identifica explicitamente os componentes de PT que ainda não estão expostos como API. O fluxo de PT implementado nesta etapa é uma fundação de domínio e persistência, não uma autorização operacional para executar serviços.

## Arquitetura

```text
Cliente web/mobile
    │ HTTPS + JWT
    ▼
SESMT.API — controllers, validação HTTP, autenticação e middleware de erro
    ▼
SESMT.Application — casos de uso, DTOs e interfaces de repositório
    ▼
SESMT.Domain — entidades, regras e transições independentes de infraestrutura
    ▲
    │ implementações EF Core
SESMT.Infrastructure — PostgreSQL/Npgsql, mapeamentos, migrations e serviços técnicos
```

- `SESMT.API`: `Program.cs` configura JWT Bearer, autorização, OpenAPI/Scalar em desenvolvimento, controllers e middleware de erros.
- `SESMT.Application`: coordena casos de uso e define interfaces consumidas pela API e infraestrutura.
- `SESMT.Domain`: contém entidades/agregados e invariantes, sem dependência do EF Core.
- `SESMT.Infrastructure`: implementa persistência EF Core, PostgreSQL, migrations e integrações.
- `SESMT.Tests`: testes unitários em xUnit; a solução também reserva diretórios para testes de integração.
- A API conecta-se ao PostgreSQL por `ConnectionStrings:Sesmt`; schema padrão `core`, além de schemas específicos já existentes (`docs` e `audit`).

As dependências apontam para dentro: API e Infrastructure dependem de Application/Domain; Domain não depende das camadas externas. Controllers não devem consultar o `DbContext` diretamente.

## Persistência e modelo de dados

O `SesmtDbContext` agrega os conjuntos de identidade, organização, força de trabalho, documentos, notificações, administração e auditoria. Mapeamentos explícitos ficam em `SESMT.Infrastructure/Persistence/Configurations`; alterações de schema são versionadas em `SESMT.Infrastructure/Persistence/Migrations`.

### PT implementada nesta etapa

| Entidade/tabela | Campos principais | Integridade e propósito |
| --- | --- | --- |
| `WorkPermit` / `core.permissao_trabalho` | `id` PK, `empresa_id` FK, `titulo`, `status`, `versao`, ator/data de liberação, início, suspensão, conclusão e cancelamento, motivos e auditoria base | FK restritiva para `core.empresa`; índice `(empresa_id, status)` para registros não excluídos; `versao` como token de concorrência otimista. |
| `WorkPermitAppointment` / `core.agendamento_pt` | Empresa, título, início/fim e solicitante | FK restritiva para empresa; término posterior ao início; nova PT exige agendamento e o índice único permite no máximo uma PT por agendamento. A FK é opcional para preservar PTs legadas. |
| `WorkPermitParticipant` / `core.permissao_trabalho_participante` | PT, usuário, responsabilidade, atribuidor e instante/versão da ciência | FKs restritivas; um usuário por PT; apenas o próprio usuário autenticado registra ciência. |
| `WorkPermitDocumentRequirement` / `core.permissao_trabalho_requisito_documental` | Empresa, serviço, tipo documental, descrição e configurador | FKs restritivas; unicidade por empresa/serviço/tipo. Nenhuma regra é pré-carregada: ausência de matriz bloqueia prontidão. |

Uma PT nasce em `EM_VERIFICACAO`. Transições registram responsável e horário; suspensão e cancelamento guardam motivo. A revisão final/aprovação documental é distinta do estado operacional, mas ainda não foi modelada nesta entrega.

### Entidades já presentes no contexto

`Company`, `Unit`, `Department`, `Position`, `Worker` e `WorkerHistory` cobrem organização e pessoas; `User`, `Role`, `Permission`, `RolePermission`, `UserRole`, `UserScope`, `UserSession` e `PasswordResetToken` cobrem acesso e sessão; `Document`, `DocumentType`, `DocumentVersion`, `DocumentLink` e `StoredFile` cobrem documentos; `AuditLog`, `Alert` e `SystemParameter` cobrem funções transversais. O módulo de PT agora tem agendamento interno, participantes com ciência individual e matriz de requisitos por empresa/serviço. Integração com calendário/sistema externo e escala corporativa não existe.

## API e contratos

### Rotas implementadas

| Método e rota | Acesso | Objetivo e respostas |
| --- | --- | --- |
| `POST /auth/login` | Público | Autentica; `200`, `401` ou `403` quando MFA é necessário. |
| `POST /auth/refresh` | Público, com refresh token | Renova sessão; `200` ou `401`. |
| `POST /auth/logout` | Token/sessão conforme controller | Encerra sessão; `204` ou `401`. |
| `GET /auth/me` | JWT | Retorna o identificador autenticado; `200` ou `401`. |
| `POST /auth/forgot-password` | Público | Retorna `202` sem revelar se o e-mail existe. |
| `POST /auth/reset-password` | Público | Redefine senha; `204` ou `400`. |
| `GET /health` | Público | Informa estado básico da API. |

### Rotas de PT

As rotas abaixo estão implementadas. Agendamento, participantes e requisitos configurados alimentam a prontidão; a liberação segue bloqueada se qualquer condição estiver ausente.

| Método e rota proposta | Objetivo | Validações/respostas esperadas |
| --- | --- | --- |
| `POST /work-permits/appointments` | Criar agendamento na empresa | Exige papel solicitante no escopo da empresa; término posterior ao início. |
| `POST /work-permits/requirements` | Configurar requisito documental por empresa/serviço | Exige role provisionada `SESMT_PT_CONFIG` atribuída à empresa; tipo documental deve estar ativo. |
| `GET /work-permits/requirements?companyId=...&serviceCode=...` | Consultar matriz configurada | Mesmo papel configurador e escopo ativo da empresa. |
| `POST /work-permits/{id}/documents` | Vincular documento existente à PT | Exige papel solicitante; documento deve pertencer à empresa; vínculo só durante verificação. |
| `POST /work-permits/{id}/participants` | Atribuir participante ativo da empresa | Exige papel solicitante; só durante verificação; usuário único por PT. |
| `POST /work-permits/{id}/acknowledge` | Registrar ciência individual | Somente o usuário autenticado atribuído à PT pode registrar; armazena versão do conteúdo e instante. |
| `POST /work-permits` | Abrir PT em verificação para agendamento e código de serviço | Exige `AppointmentId`, `ServiceCode`, título e papel `SESMT_PT_SOLICITANTE` no escopo da empresa do agendamento; `201`, `400`, `401`, `403` ou `404`. Índice único impede segunda PT para o agendamento. A abertura nunca libera serviço. |
| `GET /work-permits` | Listar PTs acessíveis | Filtra por escopos ativos de empresa no banco; `200` ou `401`. |
| `GET /work-permits/{id}` | Consultar PT e estado | Filtra por escopos ativos de empresa no banco; `200`, `401` ou `404` (oculta recurso fora do escopo). |
| `GET /work-permits/{id}/history` | Consultar trilha de transições em ordem de versão | Mesmo filtro de empresa; inclui abertura, ator, horário, status anterior/novo, ação e motivo. |
| `POST /work-permits/{id}/release` | Liberar após verificar prontidão | Exige `SESMT_PT_LIBERADOR`; `422` quando participante, matriz ou evidência vigente estiver pendente; `409` para estado inválido/concorrência. |
| `POST /work-permits/{id}/start` | Registrar início após nova validação de prontidão | Exige `SESMT_PT_OPERADOR`; revalida participante e documentos; `422` com pendências, `409` para estado inválido/concorrência. |
| `POST /work-permits/{id}/suspend` | Suspender execução com motivo obrigatório | Exige `SESMT_PT_OPERADOR`; somente `EM_EXECUCAO`; motivo não vazio; `409` ou `400`. |
| `POST /work-permits/{id}/resume` | Retomar após nova validação | Exige `SESMT_PT_OPERADOR`; revalida participante e documentos; `422` com pendências, `409` para estado inválido/concorrência. |
| `POST /work-permits/{id}/complete` | Encerrar execução | Exige `SESMT_PT_OPERADOR`; somente `EM_EXECUCAO`; `409` para estado inválido. |
| `POST /work-permits/{id}/cancel` | Cancelar antes do início com motivo obrigatório | Exige `SESMT_PT_SOLICITANTE` ou `SESMT_PT_LIBERADOR`; somente `EM_VERIFICACAO` ou `LIBERADA`; `409` ou `400`. |

Os comandos de transição devem derivar o ator do `sub` do JWT, nunca aceitar seu ID do corpo. Em conflito de `versao`, retornar `409 Conflict`. Erros devem usar `ErrorResponse` sem detalhes internos.

### Contratos detalhados de PT

Todas as rotas `/work-permits` exigem `Authorization: Bearer <access_token>`. Identificadores são UUIDs; datas/instantes usam ISO 8601 com fuso. O ator é sempre extraído do JWT.

| Rota | Entrada | Saída de sucesso | Falhas principais |
| --- | --- | --- | --- |
| `POST /work-permits/appointments` | JSON `{ companyId, title, startsAt, endsAt }` | `200` com `{ id, companyId, title, startsAt, endsAt }` | `400` título/datas inválidas; `401`; `403` sem papel/escopo; `404` empresa fora do escopo. |
| `POST /work-permits` | JSON `{ appointmentId, serviceCode, title }` | `201` com PT e status `EM_VERIFICACAO` | `400`; `401`; `403`; `404` agendamento fora do escopo; `409` agendamento já associado. |
| `POST /work-permits/requirements` | JSON `{ companyId, serviceCode, documentTypeId, description }` | `204` | `400`; `401`; `403` sem `SESMT_PT_CONFIG`; `404` empresa/tipo inexistente; `409` requisito duplicado. |
| `GET /work-permits/requirements` | Query `companyId`, `serviceCode` | `200` lista de requisitos ativos | `401`; `403`; `404` empresa fora do escopo. |
| `POST /work-permits/{id}/participants` | JSON `{ userId, responsibility }` | `200` participante, incluindo ciência (se existente) | `400`; `401`; `403`; `404` PT/usuário fora do escopo; `409` duplicidade/estado. |
| `POST /work-permits/{id}/acknowledge` | Sem corpo | `200` participante com instante e versão de ciência | `401`; `404` PT/participante não atribuído; `409` PT fora de verificação. |
| `POST /work-permits/{id}/documents` | JSON `{ documentId }` | `204` | `400`; `401`; `403`; `404` PT/documento fora da empresa; `409` duplicidade/estado. |
| `GET /work-permits`, `GET /work-permits/{id}`, `GET /work-permits/{id}/history` | Sem corpo; `id` na rota | `200` lista, PT (com participantes) ou histórico ordenado por versão | `401`; `404` item fora do escopo. |
| `POST /work-permits/{id}/release`, `/start`, `/resume`, `/complete` | Sem corpo | `200` PT atualizada | `401`; `403`; `404`; `409` estado/concorrência; `422` prontidão incompleta para release/start/resume. |
| `POST /work-permits/{id}/suspend`, `/cancel` | JSON `{ reason }` (1–1000 caracteres) | `200` PT atualizada | `400`; `401`; `403`; `404`; `409` estado/concorrência. |

O envelope de erro é `{ status, title, detail }`. Erros de integridade/concorrência retornam `409` sem expor SQL; pendências de prontidão retornam `422`.

## Catálogo de serviços e templates de PT

`GET /services` retorna altura, quente, frio, espaço confinado e eletricidade com IDs estáveis. `GET /pt-templates` lista as versões visíveis ao usuário autenticado; com `companyId`, exige escopo e papel `SESMT_PT_CONFIG`. `POST /pt-templates` recebe `companyId`, `name`, `serviceIds`, `questions`, `declaration` e `illustrative`, cria a versão 1 e `POST /pt-templates/{id}/versions` acrescenta uma versão sem alterar as anteriores. A criação e a consulta por empresa exigem `SESMT_PT_CONFIG` no escopo da empresa.

Templates armazenam serviços, declaração, perguntas (`BOOLEAN`, `TEXT` ou `NUMBER`), obrigatoriedade, impedimento e marcação ilustrativa. Até existir aprovação formal pelo SESMT, a API aceita exclusivamente templates ilustrativos. A atribuição/revogação do papel configurador continua no processo administrativo de identidade; não há endpoint de gestão de papéis.

Na abertura de uma PT dos cinco serviços do catálogo, `templateVersionIds` é obrigatório. Uma PT pode combinar versões de templates; os IDs e serviços selecionados ficam fixados na PT. `PATCH /work-permits/{id}/checklist` grava respostas apenas para perguntas dessas versões e incrementa a versão do conteúdo, exigindo nova ciência dos participantes. Liberação, início e retomada verificam respostas obrigatórias, formato e respostas impeditivas. PTs anteriores à migração mantêm seus campos e comportamento: a migração preenche seleções e respostas com listas JSON vazias, sem reescrever seu conteúdo.

## Autenticação e segurança

- Login valida credenciais e emite JWT assinado; refresh tokens são persistidos como hash, com expiração e revogação de sessão.
- O JWT valida emissor, audiência, assinatura e expiração com tolerância de relógio zero. A chave deve ter pelo menos 32 bytes e ser fornecida fora do código-fonte.
- Roles são emitidas como claims `ClaimTypes.Role`; escopos são claims `scope_type` e `scope_reference_id`, com tipos `EMPRESA`, `UNIDADE` e `SETOR`.
- Endpoints de PT exigem `[Authorize]`; o caso de uso verifica escopo ativo de empresa no banco em cada chamada. Usuário e empresa inativos, escopos removidos e recursos fora do escopo são rejeitados; escopos de unidade/setor ainda não são traduzidos em escopo de empresa.
- Papéis `SESMT_PT_SOLICITANTE`, `SESMT_PT_OPERADOR`, `SESMT_PT_LIBERADOR` e `SESMT_PT_CONFIG` são criados pela migration e verificados em `UserRole` com `CompanyId` exato. Não são concedidos automaticamente a administradores nem confiados somente a claims JWT. A atribuição/revogação por empresa permanece no processo administrativo de identidade; não existe endpoint de gestão de papéis.
- A API deve manter MFA/recuperação de senha sem revelar existência de conta, proteger segredos em configuração segura e não registrar tokens, credenciais ou dados sensíveis em logs.
- A validação de prontidão para liberação, início e retomada deve ser recalculada no servidor. Cliente, agenda, assinatura visual ou resposta enviada pelo cliente não são prova suficiente.

## Estados e invariantes do agregado de PT

```text
EM_VERIFICACAO → LIBERADA → EM_EXECUCAO → CONCLUIDA
                                      └→ SUSPENSA → EM_EXECUCAO
EM_VERIFICACAO/LIBERADA → CANCELADA
```

- Cada transição só aceita os estados de origem previstos; chamadas repetidas são recusadas.
- `Release`, `Start` e `Resume` consultam prontidão no servidor: agendamento existente; ao menos um participante ativo com ciência da versão atual; e documento ativo/vigente da empresa para cada tipo configurado. Sem participante, matriz ou evidência, a prontidão é negada. O horário do agendamento não inicia automaticamente a execução.
- Suspensão e cancelamento exigem motivo; não há cancelamento depois do início.
- Cada transição incrementa `Version`; EF Core usa esse valor como token de concorrência.
- Abertura e cada transição criam registro em `core.permissao_trabalho_historico`, consultável pelo endpoint de histórico. O modelo não oferece operações de edição; `SesmtDbContext` rejeita modificações e exclusões desses registros.

## Testes e execução

- Testes unitários verificam transições válidas e inválidas, prontidão, motivo obrigatório, ator, repetição e estado inicial.
- Executar `dotnet test SESMT.slnx` para validar a solução.
- Aplicar migrations com `dotnet ef database update --project SESMT.Infrastructure --startup-project SESMT.API` usando uma instância PostgreSQL de desenvolvimento configurada.
- Não usar os dados/credenciais de desenvolvimento para produção; não executar migration em produção sem revisão e backup.

## Pendências que impedem declarar o MVP operacional

O responsável aprovou como proposta provisória segura: papéis por empresa; uma PT por agendamento; ciência individual autenticada; bloquear liberação quando requisitos estiverem pendentes; aprovação explícita. Serviços, template e retenção devem ser configuráveis. Esta aprovação não substitui a aprovação operacional formal do responsável SESMT.

| Decisão provisória | Regra técnica nesta etapa | Limite ainda pendente |
| --- | --- | --- |
| Papéis por empresa | API confere o papel persistido vinculado à mesma empresa da PT; sem concessão automática por JWT/admin. | Endpoint e processo administrativo para conceder/revogar papéis. |
| Uma PT por agendamento | Nova PT exige agendamento; índice único impõe uma PT por agendamento. | Agenda/calendário externo não está conectado; PTs legadas sem agendamento não podem ser liberadas. |
| Ciência individual autenticada | Participante ativo da empresa; apenas o próprio usuário autenticado registra ciência e versão. | Papéis finais de participante e invalidadores da ciência (DC-07). |
| Bloquear com requisitos pendentes | Prontidão verifica participantes, matriz e evidência documental vigente; ausência bloqueia. | Critérios finais por serviço/função e responsáveis por revisão (DC-05). |
| Aprovação explícita | Abertura da PT não equivale a liberação; papel liberador deve executar a transição. | Definir segregação entre solicitante e liberador e fluxo formal de aprovação. |
| Serviços e templates configuráveis | Cinco códigos de serviço fixos; templates ilustrativos são versionados e não podem enfraquecer verificações obrigatórias/impeditivas. | Aprovação operacional dos templates pelo SESMT ainda não existe; retenção continua pendente. |

O papel `SESMT_PT_CONFIG` deve ser atribuído deliberadamente; ainda não existe tela/endpoint de gestão de papéis. A API desta entrega vincula documentos já existentes; criação/upload, aprovação e gerenciamento geral de documentos não têm controller neste repositório e precisam ser integrados para um fluxo fim a fim. A prontidão verifica status vigente, datas de validade e empresa. DC-01 a DC-14 continuam exigindo detalhamento/aceite do responsável SESMT antes de abrir piloto real. Infraestrutura, conectividade, anexos/retenção e critérios de piloto (DC-11 a DC-14) não são verificáveis apenas por testes unitários.

Faltam aprovação operacional dos templates pelo SESMT, processo administrativo para atribuir/revogar papéis (incluindo `SESMT_PT_CONFIG`), políticas de revisão/assinatura/delegação/retenção, gestão de escopos de unidade/setor, integração com agenda/calendário e equipe reais, notificações, testes de integração PostgreSQL/API e frontend. Até aprovação operacional do SESMT e validação integrada, a PT não deve autorizar trabalho real.
