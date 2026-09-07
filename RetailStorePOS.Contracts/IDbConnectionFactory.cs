using System;
using System.Data;

namespace RetailStorePOS.Contracts;

public interface IDbConnectionFactory
{
    string DatabasePath { get; }
    
    IDbConnection OpenConnection();
    
    bool IsUniqueConstraintError(Exception ex);
}
