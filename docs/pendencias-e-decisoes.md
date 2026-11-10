# Pendências de uso real: situação e decisões da equipe

Atualizado em 2026-11-10. Base: lista "O que ainda precisa ser desenvolvido/alinhado" de `PENDENCIAS_BACKEND_USO_REAL_2026-11-01.md` (frontend, `docs/handoff`).

Os PRs abaixo estão abertos e aguardam revisão. Nenhum foi mesclado.

## Situação de cada item

| # | Pendência | Situação | PRs |
| --- | --- | --- | --- |
| 1 | Política comum de acesso | Perfis `SESMT_PT_*` passam a receber `agenda:*`, `designacao:*` e `pt:*` conforme o perfil que a API exige em cada operação | backend #44 |
| 2 | Administração de usuários | Já existente: `GET /users/management-context` e `PUT /users/{id}/access`, consumidos pelo frontend nativo | — |
| 3 | Troca/desvinculação de conta | **Depende de decisão** (abaixo) | — |
| 4 | Cadastros restantes | Desativação de equipe oferecida no frontend nativo. Avaliação de impactos depende de decisão (abaixo) | frontend #12 (empilhado no #10) |
| 5 | Agenda | Criação idempotente (`Idempotency-Key`) e catálogo de responsáveis elegíveis | backend #42, #43, #45; frontend #9, #10 |
| 6 | Revisão final da PT | Aprovar/devolver, correção e reenvio com histórico | backend #46; frontend #11 |
| 7 | PDF final/snapshot | **Depende de decisão** (abaixo) | — |
| 8 | Comprovação da assinatura | **Depende de decisão** (abaixo) | — |
| 9 | Documentos específicos e outros módulos | **Depende de decisão** (abaixo) | — |
| 10 | Auditoria e jsonb | Hash v3 sobre JSON canônico; eventos antigos reportados como não verificáveis, sem recálculo | backend #47 |

Também em aberto: backend #41 (permissões de EPI e treinamento perdidas nos merges) e frontend #8 (ajustes do roteiro de verificação real).

**Ordem segura de mescla:** backend #41, #42, #43 (reapontar para `main` após #42), #44, #45, #46, #47; depois frontend #8, #9, #10, #12 (reapontar para `main` após #10) e #11.

## Decisões pedidas nos PRs abertos

1. **#43, convite público:** quem tem o link vê o nome completo dos membros da equipe (como no modo demonstração). Alternativa: primeiro nome ou iniciais.
2. **#44, PT de GESTOR e TÉCNICO:** esses perfis continuam com `pt:*` sem ter perfil de PT. A interface mostra ações que a API recusa. Remover essas permissões é uma mudança de política.
3. **#46, revisor da PT:** hoje o revisor é definido só pela designação `revisar`. Exigir também um perfil (por exemplo, Configurador)?
4. **#46, aprovação definitiva:** uma PT aprovada não pode ser reaberta. Confirmar.

## Itens que dependem de definição de produto

### 3. Troca ou desvinculação da conta de um trabalhador

O vínculo inicial existe (`PUT /workers/{id}/account-binding`). Trocar ou remover a conta afeta ciências registradas, participação em PTs em curso e o histórico. Perguntas:
- Uma ciência dada pela conta anterior continua válida para a PT em curso?
- Desvincular durante uma PT em execução deve ser bloqueado?
- A troca exige motivo e quem pode fazê-la (RH, administrador)?

### 4. Impactos da desativação de equipe

`DELETE /teams/{id}` já exige `equipe:excluir` no escopo, mas não tem controle de versão, não registra auditoria e não avalia impactos. Perguntas:
- Bloquear a desativação quando houver agendamentos futuros não cancelados com a equipe, só avisar, ou cancelá-los?
- Revogar automaticamente os convites ativos da equipe?
- Exigir `expectedVersion` e motivo, com auditoria `TEAM_DEACTIVATED`?

### 7. PDF final e snapshot da PT

O frontend nativo mostra "A versão de impressão deste contrato ainda não está disponível". Perguntas:
- Em que momento o snapshot é gerado (liberação, conclusão, aprovação da revisão)?
- O que entra: contexto, versões dos modelos, respostas, participantes, ciências, documentos vinculados, revisão?
- O PDF é gerado pela API e guardado com hash, ou montado sob demanda a partir do snapshot?
- Quem pode baixá-lo?

### 8. Comprovação da assinatura

O bloqueio de documentos sem assinatura obrigatória existe; a comprovação, não. Perguntas:
- Qual mecanismo: aceite eletrônico com login e registro de hash da versão, certificado digital (ICP-Brasil) ou provedor externo?
- A assinatura expira ou pode ser revogada?
- Uma nova versão do conteúdo invalida a assinatura anterior automaticamente?

### 9. Documentos específicos e outros módulos

Perguntas:
- Quais tipos documentais precisam de formulário próprio a partir do `MetadataSchema` e quais campos são obrigatórios?
- Escopo aprovado para fluxos nativos de AT, PET e inspeções: quais entram na próxima entrega?
- Cadastro protegido de documentos médicos ou restritos: quais perfis leem e quais registram?
