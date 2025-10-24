using System;
using System.Collections.Generic;

namespace StampliMCP.Shared.Models;

public sealed class FlowMatchingConfiguration
{
    public List<string> ActionWords { get; set; } = new();
    public List<string> EntityWords { get; set; } = new();
    public Dictionary<string, string> Aliases { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static FlowMatchingConfiguration CreateDefault()
    {
        return new FlowMatchingConfiguration
        {
            ActionWords =
            [
                "import", "export", "sync", "synchronize", "send", "get", "fetch", "create", "retrieve", "pull", "push", "submit", "release", "update", "load"
            ],
            EntityWords =
            [
                "vendor", "vendors", "supplier", "suppliers", "invoice", "invoices", "bill", "bills", "payment", "payments",
                "item", "items", "product", "products", "po", "pos", "purchase", "purchases", "order", "orders",
                "receipt", "receipts", "pr", "prs", "transaction", "transactions", "gl", "account", "accounts"
            ],
            Aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["vendor"] = "vendor",
                ["vendors"] = "vendor",
                ["supplier"] = "vendor",
                ["suppliers"] = "vendor",
                ["payee"] = "vendor",
                ["payees"] = "vendor",
                ["seller"] = "vendor",
                ["sellers"] = "vendor",
                ["bill"] = "bill",
                ["bills"] = "bill",
                ["invoice"] = "bill",
                ["invoices"] = "bill",
                ["ap"] = "bill",
                ["aptransaction"] = "bill",
                ["payment"] = "payment",
                ["payments"] = "payment",
                ["pay"] = "payment",
                ["pays"] = "payment",
                ["paying"] = "payment",
                ["product"] = "item",
                ["products"] = "item",
                ["item"] = "item",
                ["items"] = "item",
                ["sku"] = "item",
                ["skus"] = "item",
                ["inventory"] = "item",
                ["inventories"] = "item",
                ["po"] = "po",
                ["pos"] = "po",
                ["purchaseorder"] = "po",
                ["purchaseorders"] = "po",
                ["purchase"] = "po",
                ["purchases"] = "po",
                ["pr"] = "receipt",
                ["prs"] = "receipt",
                ["receipt"] = "receipt",
                ["receipts"] = "receipt",
                ["order"] = "order",
                ["orders"] = "order",
                ["transaction"] = "transaction",
                ["transactions"] = "transaction",
                ["gl"] = "account",
                ["account"] = "account",
                ["accounts"] = "account"
            }
        };
    }
}
