# Testes de integração com API e PostgreSQL

Os testes em `SESMT.Tests/Integration` usam o servidor HTTP em memória do ASP.NET Core e PostgreSQL real via Npgsql. Não usam EF InMemory ou mocks de persistência.

## Execução isolada

Configure `SESMT_TEST_POSTGRES_ADMIN` com uma conexão explícita ao banco de manutenção `postgres` de uma instância local de teste. O usuário precisa poder criar bancos. O harness admite somente `localhost`, `127.0.0.1` ou `::1`; nunca lê a connection string operacional do projeto.

```powershell
$env:SESMT_TEST_POSTGRES_ADMIN='Host=127.0.0.1;Port=<porta>;Database=postgres;Username=<usuario de teste>;Password=<senha de teste>'
dotnet test SESMT.Tests/SESMT.Tests.csproj --filter 'FullyQualifiedName~SESMT.Tests.Integration' --nologo
```

A cada execução o fixture cria um banco novo `sesmt_it_<UUID>`, aplica as migrations versionadas e insere empresas, usuários, agendamentos e PTs fictícios. Na saída, remove somente o banco que ele próprio alocou, validando o nome antes da remoção. Não altera um banco existente de aplicação. Credenciais não devem ser versionadas nem inseridas em logs.

Sem a variável, os testes são exibidos como **ignorados**, com o motivo. Isso não constitui aprovação dos testes de PostgreSQL.

## Cobertura

- Migrations desde um banco vazio e presença de histórico persistido.
- Recusa HTTP `401` sem identidade.
- Listagem limitada à empresa, contagem autorizada quando houver paginação, e `404` por URL de outra empresa.
- Busca paginada de PT combina texto, serviço e estado, preserva o total na página vazia e recusa `companyId` de outra empresa.
- Liberação sem designação retorna `403`, sem alterar estado, versão ou histórico.
- Atualizações concorrentes usam o token de versão do EF/PostgreSQL.
- Aberturas concorrentes conservam uma PT por agendamento e um evento inicial, verificando a restrição real no banco.
- Exclusão de histórico de transições é recusada.
- Duas confirmações HTTP concorrentes disputam a mesma capacidade: uma confirma, a outra recebe `409`, sem PT automática; repetir a confirmação não duplica histórico.
- Solicitações e convites recusam empresa ou participante de fora do contexto. IDs extras enviados pelo cliente não ampliam o alcance do convite.
- Convites só consultam seus próprios pedidos, não abrem a API geral, e deixam de funcionar imediatamente após revogação ou vencimento.
- Upload fica em conferência pendente, deriva o autor da sessão e recusa revisão concorrente sem criar outro arquivo; o download entrega os bytes somente ao escopo autorizado.

O handler de autenticação destes testes fornece exclusivamente a identidade fictícia; papéis, escopos e designações continuam sendo conferidos pelos serviços reais no banco. O JWT e a autenticação por senha têm seus testes próprios. O serviço de envio externo é substituído por uma implementação que recusa qualquer chamada; nenhuma configuração de e-mail é necessária.

Esta cobertura não representa o ciclo integral de aprovação, documento final/PDF, conferência de evidências, biometria ou assinatura. Esses casos devem ser acrescentados conforme seus módulos e contratos forem implementados; resultados unitários não homologam regras operacionais reais.

## Integração contínua

O workflow `.github/workflows/dotnet.yml` inicia um serviço PostgreSQL 18 descartável e fornece uma conexão de teste local com credenciais fictícias desse job. Assim, `dotnet test` executa também os testes de integração no CI. O banco `sesmt_it_<UUID>` continua sendo criado e removido pelo fixture; as configurações operacionais da API não são utilizadas.

Para desenvolvimento Windows sem Docker, a página oficial do [PostgreSQL](https://www.postgresql.org/download/windows/) referencia os [binários portáteis da EDB](https://www.enterprisedb.com/download-postgresql-binaries). O runtime e os dados temporários não fazem parte do código nem do pacote de alterações.
