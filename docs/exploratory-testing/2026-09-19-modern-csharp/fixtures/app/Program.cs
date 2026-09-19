using System;
using Shop.Orders;

var lines = new[] { new OrderLine("abc", 2, 3.5m) };
var order = new Order(Guid.NewGuid(), lines);
Console.WriteLine(order.Lines.Count);

static int Square(int number) => number * number;
Console.WriteLine(Square(4));
