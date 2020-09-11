﻿using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace xUnitRevitUtils
{
  /// <summary>
  /// Utility class with methods and properties used by the xUnit Revit plugin
  /// </summary>
  public static class xru
  {
    public static UIApplication Uiapp { get; set; }

    private static List<Action> Queue { get; set; }
    private static ExternalEvent EventHandler { get; set; }

    public static SynchronizationContext UiContext { get; set; }

    public static void Initialize(UIApplication uiapp, SynchronizationContext uiContext, ExternalEvent eventHandler, List<Action> queue)
    { 
      Uiapp = uiapp;
      UiContext = uiContext;
      EventHandler = eventHandler;
      Queue = queue;
    }

    #region utility methods


    /// <summary>
    /// Returns the selected elements in the active document
    /// </summary>
    /// <returns></returns>
    public static List<Element> GetActiveSelection()
    {
      Assert.NotNull(Uiapp);

      if (Uiapp.ActiveUIDocument != null)
        return Uiapp.ActiveUIDocument.Selection.GetElementIds().Select(x => Uiapp.ActiveUIDocument.Document.GetElement(x)).ToList();
      return new List<Element>();
    }
    /// <summary>
    /// Opens and activates a document if not open already
    /// </summary>
    /// <param name="filePath">Path to the file to open</param>
    public static Document OpenDoc(string filePath)
    {
      Assert.NotNull(Uiapp);
      Document doc = null;
      //OpenAndActivateDocument only works if run from the current context
      UiContext.Send(x => { doc = Uiapp.OpenAndActivateDocument(filePath).Document; }, null);
      Assert.NotNull(doc);
      return doc;
    }


    /// <summary>
    /// Creates a new empty document
    /// </summary>
    /// <param name="templatePath">Path to the project template</param>
    /// <param name="filePath">Path where to save the new doc</param>
    /// <param name="overwrite">If true overwrites existing files with same name</param>
    /// <returns></returns>
    public static Document CreateNewDoc(string templatePath, string filePath, bool overwrite = true)
    {
      Assert.NotNull(Uiapp);
      Document doc = null;

      try
      {
        if (overwrite && File.Exists(filePath))
          File.Delete(filePath);
      }
      catch { }

      //OpenAndActivateDocument only works if run from the current context
      UiContext.Send(x =>
      {
        //if already open, just use it
        if (!File.Exists(filePath))
        {
          doc = Uiapp.Application.NewProjectDocument(templatePath);
          doc.SaveAs(filePath);
          doc.Close();
        }

        doc = Uiapp.OpenAndActivateDocument(filePath).Document;
      }
      , null);
      Assert.NotNull(doc);
      return doc;
    }


    /// <summary>
    /// Runs an Action in a Revit transaction, uses TaskCompletionSource to communicate when done
    /// </summary>
    /// <param name="action">Action to run</param>
    /// <param name="doc">Revit Document</param>
    /// <param name="transactionName">Transaction Name</param>
    /// <returns></returns>
    public static Task RunInTransaction(Action action, Document doc, string transactionName = "transaction")
    {
      var tcs = new TaskCompletionSource<string>();
      Queue.Add(new Action(() =>
      {
        try
        {
          using (Transaction transaction = new Transaction(doc, transactionName))
          {
            transaction.Start();
            action.Invoke();
            transaction.Commit();
          }
        }
        catch (Exception e)
        {
          tcs.TrySetException(e);
        }
        tcs.TrySetResult("");
      }));

      EventHandler.Raise();

      return tcs.Task;
    }
#endregion
  }
}
