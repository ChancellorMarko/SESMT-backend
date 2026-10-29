# SESMT

## Entrega do backlog — 27/10/2026

Consulte [o relatório da entrega](docs/entrega-trello-2026-10-27.md) para os cards implementados/parciais, contratos, testes com PostgreSQL e pendências. Revisão final e PDF controlado ainda não estão implementados.

## Especificação técnica

Consulte [ENTREGA-2.md](ENTREGA-2.md) para arquitetura, modelo de dados, API, segurança e pendências do MVP.

## Conexão com o BD
Para conectar no banco de dados, será necessário utilizar o PgAdmin ou o Dbeaver (recomendado utilizar Dbeaver).

- Para conectar utilize as seguinte credênciais definidas no arquivo docker-compose.yml
    1. Host: Ip_desejado (localhost -> desenvolvimento ou homologação)
    2. BaseDeDados: sesmt
    3. Porta: 5433
    4. Nome de usuário: sesmt
    5. Senha: sesmt123

## Migrations
No projete utilizamos apenas as migrations para a criação do banco de dados. Por conta disso será necessário rodar alguns comando para que o banco esteja pronto para ser usado pela API.

- Criação inicial do Banco:

```bash
cd /home/markos/Documentos/git/SESMT-backend

dotnet ef migrations add InitialCreate \
--project SESMT.Infrastructure \
--startup-project SESMT.API \
--output-dir Persistence/Migrations
```

Na criação inicial do banco é necessária adicionar alguns modelos e pastas customizados que devem ser inseridos no arquivo de migration manualmente. Dessa forma forma inseridos os schemas de core, docs e audit e a extesão necessária pgcrypto.

- Aplicar a migration:

```bash
cd /home/markos/Documentos/git/SESMT-backend

dotnet ef database update \
--project SESMT.Infrastructure \
--startup-project SESMT.API
```

- Na migration inicial adicione os adicionais necessários:

No método up (no inicio):
```c#
// ─── Extensões obrigatórias ─────────────────────────────────────────
migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

// ─── Schemas ────────────────────────────────────────────────────────
migrationBuilder.EnsureSchema(name: "core");
migrationBuilder.EnsureSchema(name: "docs");
migrationBuilder.EnsureSchema(name: "audit");
```

No método down (no final):
```c#
// ────────────────── Schemas Customizados ───────────────────────────
migrationBuilder.DropSchema(name: "audit");
migrationBuilder.DropSchema(name: "docs");
migrationBuilder.DropSchema(name: "core");
```

