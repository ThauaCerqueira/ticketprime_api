# Histórias

## História Cliente

> Como usuário, quero cadastrar uma conta no sistema informando senha e CPF para que eu não necessite cadastrar meus dados novamente no sistema. Dado que o usuário está na tela de cadastro, quando ele informar uma senha que não atende aos requisitos, aparecerá uma mensagem "Senha obrigatoriamente precisa de no mínimo 6 caracteres ou "Campo obrigatório".

## Histórias focadas na Empresa

> Como empresa, quero limitar a venda de ingressos à capacidade total do evento para evitar superlotação e garantir a segurança e qualidade da experiência dos participantes. Dado que o evento possui uma capacidade máxima definida, quando houver ingressos disponíveis, então o sistema deve permitir a compra até atingir esse limite.

> Como empresa, quero cadastrar um cupom de desconto no sistema informando código e percentual de desconto, para tornar os preços mais competitivos no mercado. Dado que a empresa está na tela de cadastro de cupons, quando informar um código único e um percentual de desconto válido (entre 1% e 100%), então o sistema deve cadastrar o cupom com sucesso e retornar status HTTP 201.

## História focada no Administrador

> Como administrador, quero validar os dados enviados na requisição de cadastro de evento antes de salvar o evento no sistema, para garantir a integridade dos dados e evitar inconsistências no sistema. Dado que o administrador envia uma requisição de cadastro de evento com todos os campos obrigatórios preenchidos corretamente, quando a requisição for processada, então o sistema deve validar os dados e salvar o evento com sucesso, retornando status HTTP 201.

## História no formato BDD

> Cenário: Cadastro de evento pela empresa

Dado que a empresa está autenticada no sistema,
quando a empresa preencher os dados do evento (nome, data, capacidade e preço)
e confirmar o cadastro para disponibilizar eventos, para venda e gerenciar minha oferta para os clientes. Então o sistema deve registrar o evento no banco de dados e o evento deve aparecer na lista de eventos disponíveis.
