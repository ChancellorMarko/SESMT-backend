using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SESMT.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds the foreign keys that were missing between core tables (worker, unit, sector, position, user, role,
    /// permission, team, document). Non-destructive: no row is changed or removed. The constraints are created NOT VALID:
    /// they protect every new write without failing on rows that an existing database may already hold orphaned.
    /// docs/foreign-keys.md has the orphan check and the VALIDATE step. ON DELETE RESTRICT matches the code, which only
    /// soft-deletes. The relations are also declared in the model so EF inserts parents before children.
    /// </summary>
    public partial class AddMissingForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_usuario_perfil_empresa_id",
                schema: "core",
                table: "usuario_perfil",
                column: "empresa_id");

            migrationBuilder.CreateIndex(
                name: "IX_usuario_perfil_perfil_id",
                schema: "core",
                table: "usuario_perfil",
                column: "perfil_id");

            migrationBuilder.CreateIndex(
                name: "IX_trabalhador_historico_cargo_id",
                schema: "core",
                table: "trabalhador_historico",
                column: "cargo_id");

            migrationBuilder.CreateIndex(
                name: "IX_trabalhador_historico_setor_id",
                schema: "core",
                table: "trabalhador_historico",
                column: "setor_id");

            migrationBuilder.CreateIndex(
                name: "IX_trabalhador_cargo_id",
                schema: "core",
                table: "trabalhador",
                column: "cargo_id");

            migrationBuilder.CreateIndex(
                name: "IX_trabalhador_setor_id",
                schema: "core",
                table: "trabalhador",
                column: "setor_id");

            migrationBuilder.CreateIndex(
                name: "IX_trabalhador_unidade_id",
                schema: "core",
                table: "trabalhador",
                column: "unidade_id");

            migrationBuilder.CreateIndex(
                name: "IX_perfil_permissao_permissao_id",
                schema: "core",
                table: "perfil_permissao",
                column: "permissao_id");

            migrationBuilder.CreateIndex(
                name: "IX_equipe_chefe_id",
                schema: "core",
                table: "equipe",
                column: "chefe_id");

            migrationBuilder.CreateIndex(
                name: "IX_equipe_local_id",
                schema: "core",
                table: "equipe",
                column: "local_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_versao_arquivo_id",
                schema: "docs",
                table: "documento_versao",
                column: "arquivo_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_concessao_usuario_id",
                schema: "docs",
                table: "documento_concessao",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_empresa_id",
                schema: "docs",
                table: "documento",
                column: "empresa_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_setor_id",
                schema: "docs",
                table: "documento",
                column: "setor_id");

            migrationBuilder.CreateIndex(
                name: "IX_documento_unidade_id",
                schema: "docs",
                table: "documento",
                column: "unidade_id");

            migrationBuilder.Sql("ALTER TABLE core.alerta ADD CONSTRAINT \"FK_alerta_usuario_destinatario_id\" FOREIGN KEY (destinatario_id) REFERENCES core.usuario (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.cargo ADD CONSTRAINT \"FK_cargo_empresa_empresa_id\" FOREIGN KEY (empresa_id) REFERENCES core.empresa (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE docs.documento ADD CONSTRAINT \"FK_documento_documento_tipo_tipo_id\" FOREIGN KEY (tipo_id) REFERENCES docs.documento_tipo (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE docs.documento ADD CONSTRAINT \"FK_documento_empresa_empresa_id\" FOREIGN KEY (empresa_id) REFERENCES core.empresa (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE docs.documento ADD CONSTRAINT \"FK_documento_setor_setor_id\" FOREIGN KEY (setor_id) REFERENCES core.setor (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE docs.documento ADD CONSTRAINT \"FK_documento_unidade_unidade_id\" FOREIGN KEY (unidade_id) REFERENCES core.unidade (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE docs.documento_concessao ADD CONSTRAINT \"FK_documento_concessao_usuario_usuario_id\" FOREIGN KEY (usuario_id) REFERENCES core.usuario (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE docs.documento_versao ADD CONSTRAINT \"FK_documento_versao_arquivo_objeto_arquivo_id\" FOREIGN KEY (arquivo_id) REFERENCES docs.arquivo_objeto (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE docs.documento_versao ADD CONSTRAINT \"FK_documento_versao_documento_documento_id\" FOREIGN KEY (documento_id) REFERENCES docs.documento (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE docs.documento_vinculo ADD CONSTRAINT \"FK_documento_vinculo_documento_documento_id\" FOREIGN KEY (documento_id) REFERENCES docs.documento (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.equipe ADD CONSTRAINT \"FK_equipe_empresa_empresa_id\" FOREIGN KEY (empresa_id) REFERENCES core.empresa (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.equipe ADD CONSTRAINT \"FK_equipe_trabalhador_chefe_id\" FOREIGN KEY (chefe_id) REFERENCES core.trabalhador (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.equipe ADD CONSTRAINT \"FK_equipe_unidade_local_id\" FOREIGN KEY (local_id) REFERENCES core.unidade (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.perfil_permissao ADD CONSTRAINT \"FK_perfil_permissao_perfil_perfil_id\" FOREIGN KEY (perfil_id) REFERENCES core.perfil (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.perfil_permissao ADD CONSTRAINT \"FK_perfil_permissao_permissao_permissao_id\" FOREIGN KEY (permissao_id) REFERENCES core.permissao (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.setor ADD CONSTRAINT \"FK_setor_unidade_unidade_id\" FOREIGN KEY (unidade_id) REFERENCES core.unidade (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.trabalhador ADD CONSTRAINT \"FK_trabalhador_cargo_cargo_id\" FOREIGN KEY (cargo_id) REFERENCES core.cargo (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.trabalhador ADD CONSTRAINT \"FK_trabalhador_empresa_empresa_id\" FOREIGN KEY (empresa_id) REFERENCES core.empresa (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.trabalhador ADD CONSTRAINT \"FK_trabalhador_setor_setor_id\" FOREIGN KEY (setor_id) REFERENCES core.setor (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.trabalhador ADD CONSTRAINT \"FK_trabalhador_unidade_unidade_id\" FOREIGN KEY (unidade_id) REFERENCES core.unidade (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.trabalhador ADD CONSTRAINT \"FK_trabalhador_usuario_usuario_id\" FOREIGN KEY (usuario_id) REFERENCES core.usuario (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.trabalhador_historico ADD CONSTRAINT \"FK_trabalhador_historico_cargo_cargo_id\" FOREIGN KEY (cargo_id) REFERENCES core.cargo (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.trabalhador_historico ADD CONSTRAINT \"FK_trabalhador_historico_setor_setor_id\" FOREIGN KEY (setor_id) REFERENCES core.setor (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.trabalhador_historico ADD CONSTRAINT \"FK_trabalhador_historico_trabalhador_trabalhador_id\" FOREIGN KEY (trabalhador_id) REFERENCES core.trabalhador (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.unidade ADD CONSTRAINT \"FK_unidade_empresa_empresa_id\" FOREIGN KEY (empresa_id) REFERENCES core.empresa (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.usuario_escopo ADD CONSTRAINT \"FK_usuario_escopo_usuario_usuario_id\" FOREIGN KEY (usuario_id) REFERENCES core.usuario (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.usuario_perfil ADD CONSTRAINT \"FK_usuario_perfil_empresa_empresa_id\" FOREIGN KEY (empresa_id) REFERENCES core.empresa (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.usuario_perfil ADD CONSTRAINT \"FK_usuario_perfil_perfil_perfil_id\" FOREIGN KEY (perfil_id) REFERENCES core.perfil (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.usuario_perfil ADD CONSTRAINT \"FK_usuario_perfil_usuario_usuario_id\" FOREIGN KEY (usuario_id) REFERENCES core.usuario (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.usuario_sessao ADD CONSTRAINT \"FK_usuario_sessao_usuario_usuario_id\" FOREIGN KEY (usuario_id) REFERENCES core.usuario (id) ON DELETE RESTRICT NOT VALID;");

            migrationBuilder.Sql("ALTER TABLE core.usuario_token_redefinicao_senha ADD CONSTRAINT \"FK_usuario_token_redefinicao_senha_usuario_usuario_id\" FOREIGN KEY (usuario_id) REFERENCES core.usuario (id) ON DELETE RESTRICT NOT VALID;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_alerta_usuario_destinatario_id",
                schema: "core",
                table: "alerta");

            migrationBuilder.DropForeignKey(
                name: "FK_cargo_empresa_empresa_id",
                schema: "core",
                table: "cargo");

            migrationBuilder.DropForeignKey(
                name: "FK_documento_documento_tipo_tipo_id",
                schema: "docs",
                table: "documento");

            migrationBuilder.DropForeignKey(
                name: "FK_documento_empresa_empresa_id",
                schema: "docs",
                table: "documento");

            migrationBuilder.DropForeignKey(
                name: "FK_documento_setor_setor_id",
                schema: "docs",
                table: "documento");

            migrationBuilder.DropForeignKey(
                name: "FK_documento_unidade_unidade_id",
                schema: "docs",
                table: "documento");

            migrationBuilder.DropForeignKey(
                name: "FK_documento_concessao_usuario_usuario_id",
                schema: "docs",
                table: "documento_concessao");

            migrationBuilder.DropForeignKey(
                name: "FK_documento_versao_arquivo_objeto_arquivo_id",
                schema: "docs",
                table: "documento_versao");

            migrationBuilder.DropForeignKey(
                name: "FK_documento_versao_documento_documento_id",
                schema: "docs",
                table: "documento_versao");

            migrationBuilder.DropForeignKey(
                name: "FK_documento_vinculo_documento_documento_id",
                schema: "docs",
                table: "documento_vinculo");

            migrationBuilder.DropForeignKey(
                name: "FK_equipe_empresa_empresa_id",
                schema: "core",
                table: "equipe");

            migrationBuilder.DropForeignKey(
                name: "FK_equipe_trabalhador_chefe_id",
                schema: "core",
                table: "equipe");

            migrationBuilder.DropForeignKey(
                name: "FK_equipe_unidade_local_id",
                schema: "core",
                table: "equipe");

            migrationBuilder.DropForeignKey(
                name: "FK_perfil_permissao_perfil_perfil_id",
                schema: "core",
                table: "perfil_permissao");

            migrationBuilder.DropForeignKey(
                name: "FK_perfil_permissao_permissao_permissao_id",
                schema: "core",
                table: "perfil_permissao");

            migrationBuilder.DropForeignKey(
                name: "FK_setor_unidade_unidade_id",
                schema: "core",
                table: "setor");

            migrationBuilder.DropForeignKey(
                name: "FK_trabalhador_cargo_cargo_id",
                schema: "core",
                table: "trabalhador");

            migrationBuilder.DropForeignKey(
                name: "FK_trabalhador_empresa_empresa_id",
                schema: "core",
                table: "trabalhador");

            migrationBuilder.DropForeignKey(
                name: "FK_trabalhador_setor_setor_id",
                schema: "core",
                table: "trabalhador");

            migrationBuilder.DropForeignKey(
                name: "FK_trabalhador_unidade_unidade_id",
                schema: "core",
                table: "trabalhador");

            migrationBuilder.DropForeignKey(
                name: "FK_trabalhador_usuario_usuario_id",
                schema: "core",
                table: "trabalhador");

            migrationBuilder.DropForeignKey(
                name: "FK_trabalhador_historico_cargo_cargo_id",
                schema: "core",
                table: "trabalhador_historico");

            migrationBuilder.DropForeignKey(
                name: "FK_trabalhador_historico_setor_setor_id",
                schema: "core",
                table: "trabalhador_historico");

            migrationBuilder.DropForeignKey(
                name: "FK_trabalhador_historico_trabalhador_trabalhador_id",
                schema: "core",
                table: "trabalhador_historico");

            migrationBuilder.DropForeignKey(
                name: "FK_unidade_empresa_empresa_id",
                schema: "core",
                table: "unidade");

            migrationBuilder.DropForeignKey(
                name: "FK_usuario_escopo_usuario_usuario_id",
                schema: "core",
                table: "usuario_escopo");

            migrationBuilder.DropForeignKey(
                name: "FK_usuario_perfil_empresa_empresa_id",
                schema: "core",
                table: "usuario_perfil");

            migrationBuilder.DropForeignKey(
                name: "FK_usuario_perfil_perfil_perfil_id",
                schema: "core",
                table: "usuario_perfil");

            migrationBuilder.DropForeignKey(
                name: "FK_usuario_perfil_usuario_usuario_id",
                schema: "core",
                table: "usuario_perfil");

            migrationBuilder.DropForeignKey(
                name: "FK_usuario_sessao_usuario_usuario_id",
                schema: "core",
                table: "usuario_sessao");

            migrationBuilder.DropForeignKey(
                name: "FK_usuario_token_redefinicao_senha_usuario_usuario_id",
                schema: "core",
                table: "usuario_token_redefinicao_senha");

            migrationBuilder.DropIndex(
                name: "IX_usuario_perfil_empresa_id",
                schema: "core",
                table: "usuario_perfil");

            migrationBuilder.DropIndex(
                name: "IX_usuario_perfil_perfil_id",
                schema: "core",
                table: "usuario_perfil");

            migrationBuilder.DropIndex(
                name: "IX_trabalhador_historico_cargo_id",
                schema: "core",
                table: "trabalhador_historico");

            migrationBuilder.DropIndex(
                name: "IX_trabalhador_historico_setor_id",
                schema: "core",
                table: "trabalhador_historico");

            migrationBuilder.DropIndex(
                name: "IX_trabalhador_cargo_id",
                schema: "core",
                table: "trabalhador");

            migrationBuilder.DropIndex(
                name: "IX_trabalhador_setor_id",
                schema: "core",
                table: "trabalhador");

            migrationBuilder.DropIndex(
                name: "IX_trabalhador_unidade_id",
                schema: "core",
                table: "trabalhador");

            migrationBuilder.DropIndex(
                name: "IX_perfil_permissao_permissao_id",
                schema: "core",
                table: "perfil_permissao");

            migrationBuilder.DropIndex(
                name: "IX_equipe_chefe_id",
                schema: "core",
                table: "equipe");

            migrationBuilder.DropIndex(
                name: "IX_equipe_local_id",
                schema: "core",
                table: "equipe");

            migrationBuilder.DropIndex(
                name: "IX_documento_versao_arquivo_id",
                schema: "docs",
                table: "documento_versao");

            migrationBuilder.DropIndex(
                name: "IX_documento_concessao_usuario_id",
                schema: "docs",
                table: "documento_concessao");

            migrationBuilder.DropIndex(
                name: "IX_documento_empresa_id",
                schema: "docs",
                table: "documento");

            migrationBuilder.DropIndex(
                name: "IX_documento_setor_id",
                schema: "docs",
                table: "documento");

            migrationBuilder.DropIndex(
                name: "IX_documento_unidade_id",
                schema: "docs",
                table: "documento");
        }
    }
}
