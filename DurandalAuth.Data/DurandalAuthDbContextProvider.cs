﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Principal;

using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

using Breeze.ContextProvider.EF6;
using Breeze.ContextProvider;

using DurandalAuth.Domain.Model;

namespace DurandalAuth.Data
{
    /// <summary>
    /// Define here your business rules
    /// </summary>
    public class DurandalAuthDbContextProvider : EFContextProvider<DurandalAuthDbContext> 
    {
        protected UserManager<UserProfile> UserManager { get; private set; }

        public DurandalAuthDbContextProvider(UserManager<UserProfile> usermanager)
            : base() 
        {
            UserManager = usermanager;
        }
 
        /// <summary>
        /// Actions to perform before save any entity
        /// </summary>
        /// <param name="entityInfo">The entity info</param>
        /// <returns>true/false</returns>
        protected override bool BeforeSaveEntity(EntityInfo entityInfo) {

            // Add custom logic here in order to save entities
            // Return false if don´t want to  save the entity 

            // - Before saving articles we have to create the custom UrlCodeReference in order to access them from a url route
            // - Before saving articles we have to fill the Audit info

            if (entityInfo.Entity.GetType() == typeof(Article))
            {
                Article article = entityInfo.Entity as Article;                

                if (entityInfo.EntityState == EntityState.Added)
                {                    
                    article.SetUrlReference();
                    article.CreatedBy = Thread.CurrentPrincipal.Identity.GetUserName();
                    article.CreatedDate = DateTime.UtcNow;
                    article.UpdatedBy = Thread.CurrentPrincipal.Identity.GetUserName();
                    article.UpdatedDate = DateTime.UtcNow;
                }
                if (entityInfo.EntityState == EntityState.Modified)
                {
                    article.UpdatedBy = Thread.CurrentPrincipal.Identity.GetUserName();
                    article.UpdatedDate = DateTime.UtcNow;
                }                                               
            }

            // - Before saving categories we have to create the custom UrlCodeReference in order to access them from a url route

            if (entityInfo.Entity.GetType() == typeof(Category))
            {
                Category category = entityInfo.Entity as Category;
                if (entityInfo.EntityState == EntityState.Added)
                {
                    category.SetUrlReference();
                }                
            }

            return true;
       }
 
        protected override Dictionary<Type, List<EntityInfo>> BeforeSaveEntities(Dictionary<Type, List<EntityInfo>> saveMap) {

            // Add custom logic here in order to save entities

            List<EntityInfo> userprofiles;

            // - In order to save and manage accounts you need to use the AccountController and not Breeze

            if (saveMap.TryGetValue(typeof(UserProfile), out userprofiles))
            {                                
                var errors = userprofiles.Select(oi =>
                {
                    return new EFEntityError(oi, "Save Failed", "Cannot save Users using the Breeze api", "UserProfileId");
                });
                throw new EntityErrorsException(errors);
            }


            List<EntityInfo> articles;

            // - Only registered users can save articles
            // - Only article owner can save the article

            if (saveMap.TryGetValue(typeof(Article), out articles))
            {               
                if (articles.Any())
                {                    
                    // Mandatory => Registered users saving articles

                    if (!UserManager.IsInRole(Thread.CurrentPrincipal.Identity.GetUserId(), "User") || !Thread.CurrentPrincipal.Identity.IsAuthenticated)
                    {
                        var errors = articles.Select(oi =>
                        {
                            return new EFEntityError(oi, "Save Failed", "Only registered and authenticated users can save articles", "ArticleId");
                        });
                        throw new EntityErrorsException(errors);
                    }

                    // Mandatory => Only article owner can save the article

                    articles.ForEach(a =>  {
                        Article article = a.Entity as Article;
                        if (
                            (a.EntityState == EntityState.Modified || a.EntityState == EntityState.Added || a.EntityState == EntityState.Deleted) &&
                             article.CreatedBy != Thread.CurrentPrincipal.Identity.GetUserName()
                           )
                        {
                            throw new EntityErrorsException(new List<EFEntityError>() { 
                                new EFEntityError(a, "Save Failed", "You don´t have permissions for save this article", "ArticleId") 
                            });                    
                        }
                    });
                }
            }

            List<EntityInfo> categories;

            // - Only administrators can save categories

            if (saveMap.TryGetValue(typeof(Category), out categories))
            {
                if (categories.Any() && !UserManager.IsInRole(Thread.CurrentPrincipal.Identity.GetUserId(), "Administrator"))
                {
                    var errors = categories.Select(oi =>
                    {
                        return new EFEntityError(oi, "Save Failed", "Only administrators can save categories", "CategoryId");
                    });
                    throw new EntityErrorsException(errors);
                }
            }

            List<EntityInfo> tags;

            // - Only authenticated user can save tags

            if (saveMap.TryGetValue(typeof(Tag), out tags))
            {
                if (tags.Any())                                
                {
                    if (!UserManager.IsInRole(Thread.CurrentPrincipal.Identity.GetUserId(), "User") || !Thread.CurrentPrincipal.Identity.IsAuthenticated)
                    {
                        var errors = userprofiles.Select(oi =>
                        {
                            return new EFEntityError(oi, "Save Failed", "Only registered users can save tags", "TagId");
                        });
                        throw new EntityErrorsException(errors);                    
                    }
                }
            }

            return saveMap;
        }
    }
}
