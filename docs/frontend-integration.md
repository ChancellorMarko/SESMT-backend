# Integração com o frontend

## Organização dos repositórios

O projeto mantém dois repositórios:

| Repositório | Responsabilidade |
| --- | --- |
| `SESMT-backend` | API, autorização, regras de negócio, persistência, migrações, armazenamento e testes do servidor. É a única fonte do código backend. |
| `SESMT-frontend` | Interface React, navegação, formulários, validações de interação, cliente HTTP e testes da interface. |

A consolidação parte da `main` do backend no commit `ae89137` e incorpora as projeções `Browser*` que existiam na cópia versionada dentro do frontend. As implementações nativas mais recentes são preservadas. A cópia incorporada é retirada do frontend após a validação da execução conjunta; ela deixa de ser um local de desenvolvimento da API.

As branches coordenadas desta alteração são `codex/unificar-integracao-frontend`, no backend, e `codex/unificar-backend-oficial`, no frontend. Os pull requests devem referenciar um ao outro para que os contratos e a ordem de disponibilização possam ser conferidos.

## Contratos de navegador

O mesmo processo da API disponibiliza os endpoints nativos existentes e as projeções usadas pela interface. O prefixo externo da API é `/api/v1`; as projeções usam `/browser` para evitar colisões com contratos nativos. A presença desse prefixo não cria um segundo backend.

| Método | Rota externa | Finalidade |
| --- | --- | --- |
| `GET` | `/api/v1/browser/auth/csrf` | Obter o token antifalsificação vinculado ao cookie protegido. |
| `POST` | `/api/v1/browser/auth/login` | Autenticar e estabelecer a sessão do navegador. |
| `POST` | `/api/v1/browser/auth/refresh` | Renovar a sessão pelo cookie de refresh e pelo token CSRF. |
| `POST` | `/api/v1/browser/auth/logout` | Encerrar a sessão e remover o cookie de refresh. |
| `GET` | `/api/v1/browser/me` | Consultar identidade, permissões implementadas e escopos vigentes. |
| `GET` | `/api/v1/browser/organization` | Consultar a estrutura organizacional autorizada. |
| `GET` | `/api/v1/browser/workers` | Listar trabalhadores com filtros e paginação. |
| `GET` | `/api/v1/browser/workers/{id}` | Consultar o trabalhador autorizado. |
| `GET` | `/api/v1/browser/workers/{id}/movements` | Consultar as movimentações visíveis do trabalhador. |
| `GET` | `/api/v1/browser/documents` | Listar documentos visíveis com filtros e paginação. |
| `GET` | `/api/v1/browser/documents/{id}` | Consultar metadados do documento autorizado. |
| `GET` | `/api/v1/browser/documents/{id}/versions` | Consultar versões do documento autorizado. |
| `GET` | `/api/v1/browser/documents/{id}/versions/{versionId}` | Consultar metadados de uma versão autorizada. |
| `GET` | `/api/v1/browser/documents/{id}/history` | Consultar a identidade dos eventos documentais, sem expor os payloads de auditoria. |
| `GET` | `/api/v1/browser/document-types` | Consultar o catálogo de tipos visíveis. |
| `GET` | `/api/v1/browser/document-types/{id}` | Consultar um tipo visível. |

Os endpoints nativos de autenticação, documentos, trabalhadores, agenda, permissões de trabalho e demais módulos mantêm seus contratos. Alterações funcionais futuras devem ser implementadas nos serviços e políticas compartilhados do backend; projeções de navegador não devem duplicar essas regras.

## Sessão, permissões e limites

O access token JWT segue no cabeçalho `Authorization: Bearer`. O refresh token permanece em cookie `HttpOnly`, com caminho limitado à autenticação do navegador. Login, refresh e logout exigem o token CSRF no cabeçalho `X-CSRF-Token`. Em produção os cookies exigem HTTPS; a configuração de desenvolvimento permite a execução HTTP local.

As projeções expõem apenas as capacidades de consulta já implementadas: `organization.read`, `workers.read` e `documents.read`. Essas capacidades são calculadas com base nas concessões e nos escopos persistidos vigentes. Claims de perfil enviados no token não substituem a consulta de autorização ao banco, nem concedem funcionalidades adicionais.

A autorização considera empresa, unidade, setor, hierarquia ativa e, quando aplicável, o vínculo do próprio trabalhador. Um escopo de setor não passa a liberar toda a unidade. Contagens e paginação são calculadas sobre os registros autorizados.

A consulta documental é a interseção entre o escopo da projeção e a política oficial `IDocumentAccessPolicy`. As restrições vigentes para trabalhadores, lideranças e documentos protegidos continuam sendo aplicadas. A projeção atual limita-se a documentos e tipos públicos ou internos; ela não amplia a leitura de documentos restritos ou médicos. Versões e histórico dependem da autorização de leitura do documento.

As respostas dessas consultas indicam `sourceReadOnly: true` e mantêm `revision: null` quando não há revisão compatível com o contrato de edição. Dados ausentes na fonte permanecem ausentes. As projeções não criam responsáveis, estado de integração, ações de escrita, snapshots históricos ou aprovações por inferência.

Uma conta que autentica sem receber capacidades operacionais válidas continua na tela de conexão/orientação de acesso do frontend. Isso deve ser resolvido com concessões e escopos adequados no backend; o frontend não atribui permissões por conta própria. A demonstração por cargo permanece uma simulação no navegador, separada da API real.

## Execução local com dois checkouts

A organização padrão de diretórios é:

```text
workspace/
  SESMT-backend/   # clone deste repositório
  SESMT-frontend/  # clone do frontend
```

Os comandos do frontend procuram o backend em `../SESMT-backend`. Se o checkout estiver em outro local, configure `SESMT_BACKEND_PATH` no ambiente ou na configuração local de integração do frontend. O caminho deve apontar para um checkout Git fora da árvore do frontend, contendo os projetos da solução; uma cópia embutida não é aceita.

No checkout do frontend:

```sh
npm ci
npm run integration:setup
npm run integration:db
npm run integration:migrate
npm run dev:integrated
```

`integration:setup` cria a configuração privada do ambiente local. Consulte e configure esse arquivo localmente, sem versioná-lo. O banco e o armazenamento utilizados nesses comandos pertencem ao ambiente de integração local. Eles devem ser configurados de forma independente dos ambientes compartilhados.

`dev:integrated` inicia a API do checkout oficial em `http://127.0.0.1:5047` e o frontend em `http://127.0.0.1:5175`. O frontend usa o proxy de desenvolvimento para `/api/v1`, o contrato `adapter` e o prefixo `/browser`. A demonstração pode ser executada separadamente em `http://127.0.0.1:5173`.

As migrações são executadas exclusivamente a partir de `SESMT-backend`. Esta consolidação não adiciona outra migração para o setor do trabalhador: a correção correspondente já existe na sequência oficial. Um banco criado anteriormente pela cópia incorporada deve ter o histórico e o estado do esquema conferidos antes da atualização; copiar arquivos ou remover registros do histórico não substitui essa conferência.

Para validar o ambiente configurado, os comandos do frontend incluem `npm run integration:check-schema`, `npm run integration:smoke` e `npm run test:backend`. O smoke exige a aplicação integrada em execução. Os testes de navegador com persistência isolada não substituem a verificação com PostgreSQL e o fluxo HTTP real.

## Fluxo de trabalho da equipe

1. Atualizar a `main` do backend e criar uma branch para cada alteração na API. Alterações de servidor são feitas somente neste repositório.
2. Definir o contrato afetado: rota, método, entrada, saída, validações, autorização, erros e eventual migração. Uma proposta do frontend precisa ser alinhada com a implementação real antes de ser considerada disponível.
3. Implementar e testar a alteração no backend; abrir um pull request com os efeitos sobre o frontend e a persistência.
4. Adaptar o frontend em sua própria branch, referenciando o pull request e o contrato do backend. Validar as duas branches juntas antes da aprovação.
5. Disponibilizar primeiro a versão compatível da API e depois a interface que depende dela. Mudanças incompatíveis exigem uma estratégia explícita de transição.
6. Revisar e integrar cada pull request no respectivo repositório. Não há merge automático entre os dois repositórios nem sincronização por cópia de pastas.

Para um teste compartilhado, a equipe deve identificar as revisões dos dois repositórios e o ambiente da API utilizado. Isso permite reproduzir o resultado sem criar outra fonte do backend.
