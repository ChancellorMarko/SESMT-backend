using System;
using System.Collections.Generic;
using System.Net;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─── Extensões obrigatórias ─────────────────────────────────────────
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            // ─── Schemas ────────────────────────────────────────────────────────
            migrationBuilder.EnsureSchema(name: "core");
            migrationBuilder.EnsureSchema(name: "docs");
            migrationBuilder.EnsureSchema(name: "audit");

            migrationBuilder.EnsureSchema(
                name: "core");

            migrationBuilder.EnsureSchema(
                name: "docs");

            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "alerta",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    entidade_tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    entidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    severidade = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    destinatario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    canal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mensagem = table.Column<string>(type: "text", nullable: false),
                    data_disparo = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    data_leitura = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    escalonamento_nivel = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerta", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "arquivo_objeto",
                schema: "docs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_provider = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    bucket = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    object_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    nome_original = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    tamanho_bytes = table.Column<long>(type: "bigint", nullable: false),
                    hash_sha256 = table.Column<string>(type: "char(64)", nullable: false),
                    criptografado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    antivirus_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_arquivo_objeto", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cargo",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    cbo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    descricao = table.Column<string>(type: "text", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cargo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "documento",
                schema: "docs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    setor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    proprietario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    titulo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    classificacao_acesso = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    data_emissao = table.Column<DateOnly>(type: "date", nullable: false),
                    data_vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: true),
                    data_vigencia_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    palavras_chave = table.Column<string[]>(type: "text[]", nullable: false),
                    metadados = table.Column<string>(type: "jsonb", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documento", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "documento_tipo",
                schema: "docs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: true),
                    categoria = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    sigilo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    requer_assinatura = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    requer_aprovacao = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    periodicidade_dias = table.Column<int>(type: "integer", nullable: true),
                    prazo_guarda_anos = table.Column<int>(type: "integer", nullable: true),
                    metadados_schema = table.Column<string>(type: "jsonb", nullable: false),
                    versao_schema = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documento_tipo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "documento_versao",
                schema: "docs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    arquivo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    autor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    aprovador_id = table.Column<Guid>(type: "uuid", nullable: true),
                    numero_versao = table.Column<int>(type: "integer", nullable: false),
                    hash_arquivo = table.Column<string>(type: "char(64)", nullable: false),
                    justificativa = table.Column<string>(type: "text", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vigente = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    data_aprovacao = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documento_versao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "documento_vinculo",
                schema: "docs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entidade_tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    entidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documento_vinculo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "empresa",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    TipoEmpresa = table.Column<int>(type: "integer", nullable: false),
                    cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    razao_social = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    nome_fantasia = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_empresa", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "equipe",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organizacao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    local_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: true),
                    chefe_id = table.Column<Guid>(type: "uuid", nullable: true),
                    participantes_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_equipe", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "log_auditoria",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ator_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ator_email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ator_nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    acao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    recurso_tipo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    recurso_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dados_anteriores = table.Column<string>(type: "jsonb", nullable: true),
                    dados_novos = table.Column<string>(type: "jsonb", nullable: true),
                    contexto = table.Column<string>(type: "jsonb", nullable: true),
                    ip = table.Column<IPAddress>(type: "inet", nullable: true),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    hash_anterior = table.Column<string>(type: "char(64)", nullable: true),
                    hash_atual = table.Column<string>(type: "char(64)", nullable: false),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_log_auditoria", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parametro_sistema",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chave = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    valor = table.Column<string>(type: "jsonb", nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    fonte_normativa = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    vigencia_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    vigencia_fim = table.Column<DateOnly>(type: "date", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_parametro_sistema", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "perfil",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    nome = table.Column<string>(type: "varchar(200)", nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_perfil", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "perfil_permissao",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permissao_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_perfil_permissao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "permissao",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recurso = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    acao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "setor",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    descricao = table.Column<string>(type: "text", nullable: true),
                    responsavel_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_setor", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trabalhador",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    matricula = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cpf_enc = table.Column<byte[]>(type: "bytea", nullable: false),
                    cpf_hash = table.Column<string>(type: "char(64)", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    data_nascimento = table.Column<DateOnly>(type: "date", nullable: false),
                    data_admissao = table.Column<DateOnly>(type: "date", nullable: false),
                    data_demissao = table.Column<DateOnly>(type: "date", nullable: true),
                    cargo_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unidade_id = table.Column<Guid>(type: "uuid", nullable: true),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trabalhador", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trabalhador_historico",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trabalhador_id = table.Column<Guid>(type: "uuid", nullable: false),
                    setor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    cargo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_inicio = table.Column<DateOnly>(type: "date", nullable: false),
                    data_fim = table.Column<DateOnly>(type: "date", nullable: true),
                    motivo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trabalhador_historico", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "unidade",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    logradouro = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    complemento = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    bairro = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cidade = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_unidade", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuario",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nome = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    senha_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    mfa_habilitado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    mfa_secret_enc = table.Column<byte[]>(type: "bytea", nullable: true),
                    ultimo_login = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    bloqueado_ate = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    tentativas_login = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuario_escopo",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_escopo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    referencia_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_escopo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuario_perfil",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_perfil", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuario_sessao",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refresh_token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    revogado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_sessao", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuario_token_redefinicao_senha",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    expira_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    utilizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_usuario_token_redefinicao_senha", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "agendamento_pt",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    inicio_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    fim_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    solicitado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agendamento_pt", x => x.id);
                    table.ForeignKey(
                        name: "FK_agendamento_pt_empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalSchema: "core",
                        principalTable: "empresa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_agendamento_pt_usuario_solicitado_por",
                        column: x => x.solicitado_por,
                        principalSchema: "core",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permissao_trabalho_requisito_documental",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    servico_codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    tipo_documento_id = table.Column<Guid>(type: "uuid", nullable: false),
                    descricao = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    configurado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissao_trabalho_requisito_documental", x => x.id);
                    table.ForeignKey(
                        name: "FK_permissao_trabalho_requisito_documental_documento_tipo_tipo~",
                        column: x => x.tipo_documento_id,
                        principalSchema: "docs",
                        principalTable: "documento_tipo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_permissao_trabalho_requisito_documental_empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalSchema: "core",
                        principalTable: "empresa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_permissao_trabalho_requisito_documental_usuario_configurado~",
                        column: x => x.configurado_por,
                        principalSchema: "core",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permissao_trabalho",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agendamento_id = table.Column<Guid>(type: "uuid", nullable: true),
                    servico_codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    conteudo_versao = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    titulo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    versao = table.Column<int>(type: "integer", nullable: false),
                    liberada_por = table.Column<Guid>(type: "uuid", nullable: true),
                    liberada_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    iniciada_por = table.Column<Guid>(type: "uuid", nullable: true),
                    iniciada_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    suspensa_por = table.Column<Guid>(type: "uuid", nullable: true),
                    suspensa_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    motivo_suspensao = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    concluida_por = table.Column<Guid>(type: "uuid", nullable: true),
                    concluida_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    cancelada_por = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelada_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    motivo_cancelamento = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissao_trabalho", x => x.id);
                    table.ForeignKey(
                        name: "FK_permissao_trabalho_agendamento_pt_agendamento_id",
                        column: x => x.agendamento_id,
                        principalSchema: "core",
                        principalTable: "agendamento_pt",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_permissao_trabalho_empresa_empresa_id",
                        column: x => x.empresa_id,
                        principalSchema: "core",
                        principalTable: "empresa",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permissao_trabalho_historico",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    permissao_trabalho_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status_anterior = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    status_novo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    acao = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ocorrido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: false),
                    motivo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    versao = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissao_trabalho_historico", x => x.id);
                    table.ForeignKey(
                        name: "FK_permissao_trabalho_historico_permissao_trabalho_permissao_t~",
                        column: x => x.permissao_trabalho_id,
                        principalSchema: "core",
                        principalTable: "permissao_trabalho",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "permissao_trabalho_participante",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    permissao_trabalho_id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    responsabilidade = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    atribuido_por = table.Column<Guid>(type: "uuid", nullable: false),
                    ciente_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    ciente_versao = table.Column<int>(type: "integer", nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    criado_em = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    criado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    atualizado_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    atualizado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    excluido_em = table.Column<DateTimeOffset>(type: "timestamptz", nullable: true),
                    excluido_por = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissao_trabalho_participante", x => x.id);
                    table.ForeignKey(
                        name: "FK_permissao_trabalho_participante_permissao_trabalho_permissa~",
                        column: x => x.permissao_trabalho_id,
                        principalSchema: "core",
                        principalTable: "permissao_trabalho",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_permissao_trabalho_participante_usuario_atribuido_por",
                        column: x => x.atribuido_por,
                        principalSchema: "core",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_permissao_trabalho_participante_usuario_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "core",
                        principalTable: "usuario",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_agendamento_pt_empresa_inicio",
                schema: "core",
                table: "agendamento_pt",
                columns: new[] { "empresa_id", "inicio_em" },
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_agendamento_pt_solicitado_por",
                schema: "core",
                table: "agendamento_pt",
                column: "solicitado_por");

            migrationBuilder.CreateIndex(
                name: "ix_alerta_destinatario_status",
                schema: "core",
                table: "alerta",
                columns: new[] { "destinatario_id", "status", "data_disparo" });

            migrationBuilder.CreateIndex(
                name: "ix_arquivo_hash",
                schema: "docs",
                table: "arquivo_objeto",
                column: "hash_sha256");

            migrationBuilder.CreateIndex(
                name: "ux_cargo_empresa_nome",
                schema: "core",
                table: "cargo",
                columns: new[] { "empresa_id", "nome" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_documento_metadados",
                schema: "docs",
                table: "documento",
                column: "metadados")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_documento_palavras_chave",
                schema: "docs",
                table: "documento",
                column: "palavras_chave")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_documento_status",
                schema: "docs",
                table: "documento",
                column: "status",
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_documento_vigencia",
                schema: "docs",
                table: "documento",
                column: "data_vigencia_fim",
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_documento_tipo_codigo_unidade",
                schema: "docs",
                table: "documento",
                columns: new[] { "tipo_id", "codigo", "unidade_id" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_documento_tipo_codigo",
                schema: "docs",
                table: "documento_tipo",
                column: "codigo",
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_documento_versao_numero",
                schema: "docs",
                table: "documento_versao",
                columns: new[] { "documento_id", "numero_versao" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_documento_versao_vigente",
                schema: "docs",
                table: "documento_versao",
                column: "documento_id",
                unique: true,
                filter: "vigente = true AND excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_documento_vinculo_entidade",
                schema: "docs",
                table: "documento_vinculo",
                columns: new[] { "entidade_tipo", "entidade_id" },
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_documento_vinculo",
                schema: "docs",
                table: "documento_vinculo",
                columns: new[] { "documento_id", "entidade_tipo", "entidade_id" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_empresa_cnpj",
                schema: "core",
                table: "empresa",
                column: "cnpj",
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_equipe_empresa",
                schema: "core",
                table: "equipe",
                column: "empresa_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipe_organizacao",
                schema: "core",
                table: "equipe",
                column: "organizacao_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipe_participantes",
                schema: "core",
                table: "equipe",
                column: "participantes_ids")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ux_equipe_organizacao_nome",
                schema: "core",
                table: "equipe",
                columns: new[] { "organizacao_id", "nome" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_ator",
                schema: "audit",
                table: "log_auditoria",
                columns: new[] { "ator_id", "ocorrido_em" });

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_hash_atual",
                schema: "audit",
                table: "log_auditoria",
                column: "hash_atual");

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_ocorrido_em",
                schema: "audit",
                table: "log_auditoria",
                column: "ocorrido_em");

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_recurso",
                schema: "audit",
                table: "log_auditoria",
                columns: new[] { "recurso_tipo", "recurso_id" });

            migrationBuilder.CreateIndex(
                name: "ux_parametro_chave",
                schema: "core",
                table: "parametro_sistema",
                column: "chave",
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_perfil_codigo",
                schema: "core",
                table: "perfil",
                column: "codigo",
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_perfil_permissao",
                schema: "core",
                table: "perfil_permissao",
                columns: new[] { "perfil_id", "permissao_id" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_permissao_recurso_acao",
                schema: "core",
                table: "permissao",
                columns: new[] { "recurso", "acao" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_permissao_trabalho_empresa_status",
                schema: "core",
                table: "permissao_trabalho",
                columns: new[] { "empresa_id", "status" },
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_permissao_trabalho_agendamento",
                schema: "core",
                table: "permissao_trabalho",
                column: "agendamento_id",
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_permissao_trabalho_historico_versao",
                schema: "core",
                table: "permissao_trabalho_historico",
                columns: new[] { "permissao_trabalho_id", "versao" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_permissao_trabalho_participante_atribuido_por",
                schema: "core",
                table: "permissao_trabalho_participante",
                column: "atribuido_por");

            migrationBuilder.CreateIndex(
                name: "IX_permissao_trabalho_participante_usuario_id",
                schema: "core",
                table: "permissao_trabalho_participante",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ux_permissao_trabalho_participante",
                schema: "core",
                table: "permissao_trabalho_participante",
                columns: new[] { "permissao_trabalho_id", "usuario_id" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_permissao_trabalho_requisito_documental_configurado_por",
                schema: "core",
                table: "permissao_trabalho_requisito_documental",
                column: "configurado_por");

            migrationBuilder.CreateIndex(
                name: "IX_permissao_trabalho_requisito_documental_tipo_documento_id",
                schema: "core",
                table: "permissao_trabalho_requisito_documental",
                column: "tipo_documento_id");

            migrationBuilder.CreateIndex(
                name: "ux_pt_requisito_servico_tipo_doc",
                schema: "core",
                table: "permissao_trabalho_requisito_documental",
                columns: new[] { "empresa_id", "servico_codigo", "tipo_documento_id" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_setor_unidade_nome",
                schema: "core",
                table: "setor",
                columns: new[] { "unidade_id", "nome" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_trabalhador_ativo",
                schema: "core",
                table: "trabalhador",
                column: "ativo");

            migrationBuilder.CreateIndex(
                name: "ux_trabalhador_empresa_cpf_hash",
                schema: "core",
                table: "trabalhador",
                columns: new[] { "empresa_id", "cpf_hash" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_trabalhador_empresa_matricula",
                schema: "core",
                table: "trabalhador",
                columns: new[] { "empresa_id", "matricula" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_historico_trabalhador",
                schema: "core",
                table: "trabalhador_historico",
                columns: new[] { "trabalhador_id", "data_inicio" });

            migrationBuilder.CreateIndex(
                name: "ux_unidade_empresa_codigo",
                schema: "core",
                table: "unidade",
                columns: new[] { "empresa_id", "codigo" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_ativo",
                schema: "core",
                table: "usuario",
                column: "ativo");

            migrationBuilder.CreateIndex(
                name: "ux_usuario_email",
                schema: "core",
                table: "usuario",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_usuario_escopo",
                schema: "core",
                table: "usuario_escopo",
                columns: new[] { "usuario_id", "tipo_escopo", "referencia_id" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_usuario_perfil",
                schema: "core",
                table: "usuario_perfil",
                columns: new[] { "usuario_id", "perfil_id", "empresa_id" },
                unique: true,
                filter: "excluido_em IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_sessao_usuario",
                schema: "core",
                table: "usuario_sessao",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ux_usuario_sessao_refresh_hash",
                schema: "core",
                table: "usuario_sessao",
                column: "refresh_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuario_token_redefinicao_usuario",
                schema: "core",
                table: "usuario_token_redefinicao_senha",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ux_usuario_token_redefinicao_hash",
                schema: "core",
                table: "usuario_token_redefinicao_senha",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerta",
                schema: "core");

            migrationBuilder.DropTable(
                name: "arquivo_objeto",
                schema: "docs");

            migrationBuilder.DropTable(
                name: "cargo",
                schema: "core");

            migrationBuilder.DropTable(
                name: "documento",
                schema: "docs");

            migrationBuilder.DropTable(
                name: "documento_versao",
                schema: "docs");

            migrationBuilder.DropTable(
                name: "documento_vinculo",
                schema: "docs");

            migrationBuilder.DropTable(
                name: "equipe",
                schema: "core");

            migrationBuilder.DropTable(
                name: "log_auditoria",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "parametro_sistema",
                schema: "core");

            migrationBuilder.DropTable(
                name: "perfil",
                schema: "core");

            migrationBuilder.DropTable(
                name: "perfil_permissao",
                schema: "core");

            migrationBuilder.DropTable(
                name: "permissao",
                schema: "core");

            migrationBuilder.DropTable(
                name: "permissao_trabalho_historico",
                schema: "core");

            migrationBuilder.DropTable(
                name: "permissao_trabalho_participante",
                schema: "core");

            migrationBuilder.DropTable(
                name: "permissao_trabalho_requisito_documental",
                schema: "core");

            migrationBuilder.DropTable(
                name: "setor",
                schema: "core");

            migrationBuilder.DropTable(
                name: "trabalhador",
                schema: "core");

            migrationBuilder.DropTable(
                name: "trabalhador_historico",
                schema: "core");

            migrationBuilder.DropTable(
                name: "unidade",
                schema: "core");

            migrationBuilder.DropTable(
                name: "usuario_escopo",
                schema: "core");

            migrationBuilder.DropTable(
                name: "usuario_perfil",
                schema: "core");

            migrationBuilder.DropTable(
                name: "usuario_sessao",
                schema: "core");

            migrationBuilder.DropTable(
                name: "usuario_token_redefinicao_senha",
                schema: "core");

            migrationBuilder.DropTable(
                name: "permissao_trabalho",
                schema: "core");

            migrationBuilder.DropTable(
                name: "documento_tipo",
                schema: "docs");

            migrationBuilder.DropTable(
                name: "agendamento_pt",
                schema: "core");

            migrationBuilder.DropTable(
                name: "empresa",
                schema: "core");

            migrationBuilder.DropTable(
                name: "usuario",
                schema: "core");

            // ────────────────── Schemas Customizados ───────────────────────────
            migrationBuilder.DropSchema(name: "audit");
            migrationBuilder.DropSchema(name: "docs");
            migrationBuilder.DropSchema(name: "core");
        }
    }
}
