# Histórias

## História Cliente

> Como usuário, quero cadastrar uma conta no sistema informando senha e CPF.

## Histórias focadas na Empresa

> Como empresa, quero limitar a venda de ingressos à capacidade total do evento.

> Como empresa, quero cadastrar um cupom de desconto no sistema informando código e percentual de desconto.

## História focada no Administrador

> Como administrador, quero validar os dados enviados na requisição de cadastro de evento antes de salvar o evento no sistema.

## História no formato BDD

> Cenário: Cadastro de evento pela empresa

Dado que a empresa está autenticada no sistema
quando a empresa preencher os dados do evento (nome, data, capacidade e preço)
e confirmar o cadastro
então o sistema deve registrar o evento no banco de dados
e o evento deve aparecer na lista de eventos disponíveis.
