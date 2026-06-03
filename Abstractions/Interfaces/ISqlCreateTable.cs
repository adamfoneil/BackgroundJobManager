namespace Abstractions.Interfaces;

public interface ISqlCreateTable
{
    string IfNotExists(string tableName);
}
