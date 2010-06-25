﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Mvc;
using StackExchange.DataExplorer.Helpers;
using StackExchange.DataExplorer.Models;

namespace StackExchange.DataExplorer.Controllers
{
    public class QueryController : StackOverflowController
    {
        [HttpPost]
        [Route("query/{siteId}")]
        public ActionResult Execute(string sql, int siteId, string resultsToText, int? savedQueryId)
        {
            Site site = Current.DB.Sites.Where(s => s.Id == siteId).First();
            ActionResult rval;

            if (savedQueryId != null)
            {
                sql = Current.DB.SavedQueries.First(q => q.Id == savedQueryId.Value).Query.QueryBody;
            }

            var parsedQuery = new ParsedQuery(sql, Request.Params);

            try
            {
                if (!parsedQuery.AllParamsSet)
                {
                    if (!string.IsNullOrEmpty(parsedQuery.ErrorMessage))
                    {
                        throw new ApplicationException(parsedQuery.ErrorMessage);
                    }

                    throw new ApplicationException("All parameters must be set!");
                }

                string json = QueryRunner.GetJson(parsedQuery, site, CurrentUser);
                if (resultsToText == "true")
                {
                    json = QueryResults.FromJson(json).ToTextResults().ToJson();
                }

                rval = Content(json, "application/json");
                QueryRunner.LogQueryExecution(CurrentUser, site, parsedQuery);
            }
            catch (Exception e)
            {
                var result = new Dictionary<string, string>();
                var sqlException = e as SqlException;
                if (sqlException != null)
                {
                    result["error"] = sqlException.Message;
                }
                else
                {
                    result["error"] = e.Message;
                }
                rval = Json(result);
            }

            return rval;
        }


        [Route(@"{sitename}/csv/{queryId:\d+}/{slug?}", RoutePriority.Low)]
        public ActionResult ShowCsv(string sitename, int queryId)
        {
            Query query = FindQuery(queryId);

            if (query == null)
            {
                return PageNotFound();
            }

            TrackQueryView(queryId);
            CachedResult cachedResults = GetCachedResults(query);
            return new CsvResult(cachedResults.Results);
        }

        [Route(@"{sitename}/qte/{savedQueryId:\d+}/{slug?}", RoutePriority.Low)]
        public ActionResult EditText(string sitename, int savedQueryId)
        {
            SetCommonQueryViewData(sitename);
            SetHeaderInfo(savedQueryId);

            SavedQuery savedQuery = FindSavedQuery(savedQueryId);

            if (savedQuery == null)
            {
                return PageNotFound();
            }

            savedQuery.UpdateQueryBodyComment();

            ViewData["query"] = savedQuery.Query;

            CachedResult cachedResults = GetCachedResults(savedQuery.Query);

            if (cachedResults != null && cachedResults.Results != null)
            {
                cachedResults.Results = QueryResults.FromJson(cachedResults.Results).ToTextResults().ToJson();
            }

            ViewData["cached_results"] = cachedResults;

            return View("New", Site);
        }

        [Route(@"{sitename}/qe/{savedQueryId:\d+}/{slug?}", RoutePriority.Low)]
        public ActionResult Edit(string sitename, int savedQueryId)
        {
            SetCommonQueryViewData(sitename);
            SetHeaderInfo(savedQueryId);

            SavedQuery savedQuery = FindSavedQuery(savedQueryId);

            if (savedQuery == null)
            {
                return PageNotFound();
            }

            savedQuery.UpdateQueryBodyComment();

            ViewData["query"] = savedQuery.Query;
            ViewData["cached_results"] = GetCachedResults(savedQuery.Query);

            return View("New", Site);
        }


        [Route(@"{sitename}/qt/{queryId:\d+}/{slug?}", RoutePriority.Low)]
        public ActionResult ShowText(string sitename, int queryId)
        {
            SetCommonQueryViewData(sitename);

            Query query = FindQuery(queryId);
            if (query == null)
            {
                return PageNotFound();
            }

            TrackQueryView(queryId);

            ViewData["query"] = query;
            CachedResult cachedResults = GetCachedResults(query);
            if (cachedResults != null && cachedResults.Results != null)
            {
                cachedResults.Results = QueryResults.FromJson(cachedResults.Results).ToTextResults().ToJson();
            }

            ViewData["cached_results"] = cachedResults;
            return View("New", Site);
        }

        [Route(@"{sitename}/q/{queryId:\d+}/{slug?}", RoutePriority.Low)]
        public ActionResult Show(string sitename, int queryId)
        {
            SetCommonQueryViewData(sitename);
            Query query = FindQuery(queryId);
            if (query == null)
            {
                return PageNotFound();
            }

            ViewData["query"] = query;
            TrackQueryView(queryId);
            ViewData["cached_results"] = GetCachedResults(query);
            return View("New", Site);
        }

        [Route("{sitename}/query/new", RoutePriority.Low)]
        public ActionResult New(string sitename)
        {
            SetCommonQueryViewData(sitename);
            return View(Site);
        }

        private void SetCommonQueryViewData(string sitename)
        {
            SetHeaderInfo();
            Site = GetSite(sitename);

            SelectMenuItem("Compose Query");

            ViewData["GuessedUserId"] = Site.GuessUserId(CurrentUser);
            ViewData["Tables"] = Site.GetTableInfos();
            ViewData["Sites"] = Current.DB.Sites.ToList();
        }

        private void TrackQueryView(int id)
        {
            if (!IsSearchEngine())
            {
                QueryViewTracker.TrackQueryView(GetRemoteIP(), id);
            }
        }


        private void SetHeaderInfo()
        {
            SetHeaderInfo(null);
        }

        private Query FindQuery(int id)
        {
            return Current.DB.Queries.FirstOrDefault(q => q.Id == id);
        }

        private SavedQuery FindSavedQuery(int id)
        {
            return Current.DB.SavedQueries.FirstOrDefault(s => s.Id == id);
        }

        private void SetHeaderInfo(int? edit)
        {
            if (edit != null)
            {
                SetHeader("Editing Query");
                ViewData["SavedQueryId"] = edit.Value;
            }
            else
            {
                SetHeader("Compose Query");
            }
        }
    }
}